using System.Globalization;
using System.Runtime.InteropServices;
using System.Text.Json;

namespace Outlook;

internal static class OutlookInterop
{
    // OlDefaultFolders
    private const int OlFolder_Inbox = 6;
    private const int OlFolder_Sent = 5;
    private const int OlFolder_Drafts = 16;
    private const int OlFolder_Junk = 23;
    private const int OlFolder_Deleted = 3;
    private const int OlFolder_Calendar = 9;

    // OlItemType
    private const int OlItemType_Mail = 0;

    private sealed record StoreDto(string Name);

    private sealed record FolderDto(string Name, string FullPath, int DefaultItemType);

    private sealed record MailDto(
        string EntryId,
        string Subject,
        string SenderName,
        string SenderEmail,
        string To,
        string Cc,
        DateTime? ReceivedTime,
        bool Unread,
        int Size,
        string? Body,
        string? InternetMessageHeaders
    );

    private sealed record DraftResultDto(string EntryId, string Subject, string SavedInFolder);

    private sealed record ApptDto(string EntryId, string Subject, string Location, DateTime Start, DateTime End, bool AllDayEvent);

    public static string ListStores()
        => StaThread.Run(() =>
        {
            object? app = null;
            object? ns = null;
            try
            {
                (app, ns) = GetOutlook();
                dynamic dns = ns!;
                var stores = new List<StoreDto>();

                foreach (var rootObj in (System.Collections.IEnumerable)dns.Folders)
                {
                    dynamic root = rootObj!;
                    try { stores.Add(new StoreDto(SafeStr(() => (string)root.Name) ?? "")); }
                    finally { ReleaseCom(rootObj); }
                }

                return ToJson(stores);
            }
            finally
            {
                ReleaseCom(ns);
                ReleaseCom(app);
            }
        });

    public static string GetDefaultStore()
        => StaThread.Run(() =>
        {
            object? app = null;
            object? ns = null;
            object? storeObj = null;

            try
            {
                (app, ns) = GetOutlook();
                dynamic dns = ns!;
                storeObj = dns.DefaultStore;
                dynamic store = storeObj;

                var name = SafeStr(() => (string)store.DisplayName) ?? SafeStr(() => (string)store.FilePath) ?? "";
                return ToJson(new { name });
            }
            finally
            {
                ReleaseCom(storeObj);
                ReleaseCom(ns);
                ReleaseCom(app);
            }
        });

    public static string ListFolders2(string storeName, string folderSpec)
        => StaThread.Run(() =>
        {
            if (string.IsNullOrWhiteSpace(storeName))
                throw new ArgumentException("store_name ist leer.");

            object? app = null;
            object? ns = null;
            object? folder = null;

            try
            {
                (app, ns) = GetOutlook();
                folder = ResolveFolderByStoreAndSpec(ns!, storeName, folderSpec);
                dynamic dfolder = folder!;

                var list = new List<FolderDto>();
                foreach (var fObj in (System.Collections.IEnumerable)dfolder.Folders)
                {
                    dynamic f = fObj!;
                    try
                    {
                        string name = SafeStr(() => (string)f.Name) ?? "";
                        int defaultItemType = SafeInt(() => (int)f.DefaultItemType);
                        string full = $"{storeName}\\{NormalizeFolderSpec(folderSpec)}\\{name}".TrimEnd('\\');
                        list.Add(new FolderDto(name, full, defaultItemType));
                    }
                    finally { ReleaseCom(fObj); }
                }

                return ToJson(list);
            }
            finally
            {
                ReleaseCom(folder);
                ReleaseCom(ns);
                ReleaseCom(app);
            }
        });

    public static string ListMails(string storeName, string folderSpec, int skip, int max, bool includeBody, int bodyMaxChars)
        => StaThread.Run(() =>
        {
            if (string.IsNullOrWhiteSpace(storeName))
                throw new ArgumentException("store_name ist leer.");

            object? app = null;
            object? ns = null;
            object? folder = null;
            object? items = null;

            try
            {
                (app, ns) = GetOutlook();
                folder = ResolveFolderByStoreAndSpec(ns!, storeName, folderSpec);
                dynamic dfolder = folder!;

                items = dfolder.Items;
                dynamic ditems = items!;
                ditems.Sort("[ReceivedTime]", true);

                var result = new List<MailDto>();

                int i = 0;
                object? itemObj;
                try { itemObj = ditems.GetFirst(); } catch { itemObj = null; }

                while (itemObj != null && result.Count < max)
                {
                    dynamic item = itemObj;
                    try
                    {
                        var subjectMaybe = SafeStr(() => (string)item.Subject);
                        if (subjectMaybe is null) continue;

                        if (i++ < skip) continue;

                        result.Add(MapMail(item, includeBody, bodyMaxChars, includeHeaders: false, headersMaxChars: 0));
                    }
                    finally
                    {
                        var prev = itemObj;
                        try { itemObj = ditems.GetNext(); } catch { itemObj = null; }
                        ReleaseCom(prev);
                    }
                }

                return ToJson(result);
            }
            finally
            {
                ReleaseCom(items);
                ReleaseCom(folder);
                ReleaseCom(ns);
                ReleaseCom(app);
            }
        });

    public static string GetMailByEntryId(string entryId, string? storeName, bool includeBody, int bodyMaxChars, bool includeHeaders, int headersMaxChars)
        => StaThread.Run(() =>
        {
            if (string.IsNullOrWhiteSpace(entryId))
                throw new ArgumentException("entry_id ist leer.");

            object? app = null;
            object? ns = null;
            object? itemObj = null;
            object? storeObj = null;

            try
            {
                (app, ns) = GetOutlook();
                dynamic dns = ns!;

                if (!string.IsNullOrWhiteSpace(storeName))
                {
                    storeObj = FindStoreObject(dns, storeName!);
                    dynamic store = storeObj;
                    string storeId = SafeStr(() => (string)store.StoreID) ?? "";
                    itemObj = dns.GetItemFromID(entryId, storeId);
                }
                else
                {
                    itemObj = dns.GetItemFromID(entryId);
                }

                if (itemObj == null) throw new InvalidOperationException("Item nicht gefunden.");

                dynamic item = itemObj;
                var subjectMaybe = SafeStr(() => (string)item.Subject);
                if (subjectMaybe is null)
                    throw new InvalidOperationException("EntryId gehört nicht zu einer MailItem (oder Zugriff verweigert).");

                var dto = MapMail(item, includeBody, bodyMaxChars, includeHeaders, headersMaxChars);
                return ToJson(dto);
            }
            finally
            {
                ReleaseCom(itemObj);
                ReleaseCom(storeObj);
                ReleaseCom(ns);
                ReleaseCom(app);
            }
        });

    public static string SearchMails2(
        string storeName,
        string folderSpec,
        string query,
        string subjectContains,
        string senderContains,
        string toOrCcContains,
        bool unreadOnly,
        string? receivedAfterIso,
        string? receivedBeforeIso,
        int max,
        bool includeBody,
        int bodyMaxChars)
        => StaThread.Run(() =>
        {
            if (string.IsNullOrWhiteSpace(storeName))
                throw new ArgumentException("store_name ist leer.");

            DateTime? after = ParseIsoOrNull(receivedAfterIso);
            DateTime? before = ParseIsoOrNull(receivedBeforeIso);

            object? app = null;
            object? ns = null;
            object? folder = null;
            object? items = null;

            try
            {
                (app, ns) = GetOutlook();
                folder = ResolveFolderByStoreAndSpec(ns!, storeName, folderSpec);
                dynamic dfolder = folder!;

                items = dfolder.Items;
                dynamic ditems = items!;
                ditems.Sort("[ReceivedTime]", true);

                var q = (query ?? "").Trim();
                var subjF = (subjectContains ?? "").Trim();
                var sndF = (senderContains ?? "").Trim();
                var toccF = (toOrCcContains ?? "").Trim();

                var result = new List<MailDto>();

                object? itemObj;
                try { itemObj = ditems.GetFirst(); } catch { itemObj = null; }

                while (itemObj != null && result.Count < max)
                {
                    dynamic item = itemObj;
                    try
                    {
                        var subjectMaybe = SafeStr(() => (string)item.Subject);
                        if (subjectMaybe is null) continue;

                        DateTime? received = SafeDate(() => (DateTime)item.ReceivedTime);
                        if (after.HasValue && received.HasValue && received.Value < after.Value) continue;
                        if (before.HasValue && received.HasValue && received.Value > before.Value) continue;

                        if (unreadOnly && !SafeBool(() => (bool)item.UnRead)) continue;

                        string subject = subjectMaybe ?? "";
                        string sender = SafeStr(() => (string)item.SenderName) ?? "";
                        string to = SafeStr(() => (string)item.To) ?? "";
                        string cc = SafeStr(() => (string)item.CC) ?? "";
                        string bodyRaw = SafeStr(() => (string)item.Body) ?? "";

                        bool hit =
                            (q.Length == 0 || ContainsCI(subject, q) || ContainsCI(sender, q) || ContainsCI(bodyRaw, q)) &&
                            (subjF.Length == 0 || ContainsCI(subject, subjF)) &&
                            (sndF.Length == 0 || ContainsCI(sender, sndF)) &&
                            (toccF.Length == 0 || ContainsCI(to, toccF) || ContainsCI(cc, toccF));

                        if (!hit) continue;

                        result.Add(MapMail(item, includeBody, bodyMaxChars, includeHeaders: false, headersMaxChars: 0));
                    }
                    finally
                    {
                        var prev = itemObj;
                        try { itemObj = ditems.GetNext(); } catch { itemObj = null; }
                        ReleaseCom(prev);
                    }
                }

                return ToJson(result);
            }
            finally
            {
                ReleaseCom(items);
                ReleaseCom(folder);
                ReleaseCom(ns);
                ReleaseCom(app);
            }
        });

    public static string CreateDraft2(string storeName, string saveFolderSpec, string to, string cc, string bcc, string subject, string body, bool isHtml)
        => StaThread.Run(() =>
        {
            if (string.IsNullOrWhiteSpace(storeName))
                throw new ArgumentException("store_name ist leer.");

            object? app = null;
            object? ns = null;
            object? mailObj = null;
            object? saveFolderObj = null;
            object? movedObj = null;

            try
            {
                (app, ns) = GetOutlook();
                dynamic dapp = app!;

                mailObj = dapp.CreateItem(OlItemType_Mail);
                dynamic mail = mailObj!;
                mail.To = to ?? "";
                mail.CC = cc ?? "";
                mail.BCC = bcc ?? "";
                mail.Subject = subject ?? "";

                if (isHtml) mail.HTMLBody = body ?? "";
                else mail.Body = body ?? "";

                // Save first so EntryID exists
                mail.Save();

                saveFolderObj = ResolveFolderByStoreAndSpec(ns!, storeName, saveFolderSpec);
                dynamic saveFolder = saveFolderObj!;

                movedObj = mail.Move(saveFolder);
                dynamic moved = movedObj!;

                string entryId = SafeStr(() => (string)moved.EntryID) ?? "";
                return ToJson(new DraftResultDto(entryId, subject ?? "", $"{storeName}\\{NormalizeFolderSpec(saveFolderSpec)}"));
            }
            finally
            {
                ReleaseCom(movedObj);
                ReleaseCom(saveFolderObj);
                ReleaseCom(mailObj);
                ReleaseCom(ns);
                ReleaseCom(app);
            }
        });

    public static string SearchCalendar2(string storeName, string folderSpec, string startIso, string endIso, string query, int max)
        => StaThread.Run(() =>
        {
            if (string.IsNullOrWhiteSpace(storeName))
                throw new ArgumentException("store_name ist leer.");

            if (!DateTime.TryParse(startIso, CultureInfo.InvariantCulture, DateTimeStyles.AssumeLocal, out var start))
                throw new ArgumentException("start ist kein gültiges ISO Datum/Uhrzeit.");
            if (!DateTime.TryParse(endIso, CultureInfo.InvariantCulture, DateTimeStyles.AssumeLocal, out var end))
                throw new ArgumentException("end ist kein gültiges ISO Datum/Uhrzeit.");
            if (end <= start) throw new ArgumentException("end muss nach start liegen.");

            object? app = null;
            object? ns = null;
            object? folder = null;
            object? items = null;
            object? restricted = null;

            try
            {
                (app, ns) = GetOutlook();
                folder = ResolveFolderByStoreAndSpec(ns!, storeName, folderSpec);
                dynamic dcal = folder!;

                items = dcal.Items;
                dynamic ditems = items!;
                ditems.IncludeRecurrences = true;
                ditems.Sort("[Start]");

                // Achtung: Outlook Restrict ist teils locale-abhängig.
                // Wir übernehmen hier bewusst den Ansatz aus deinem vorhandenen Plugin.
                var startStr = start.ToString("MM/dd/yyyy HH:mm", CultureInfo.InvariantCulture);
                var endStr = end.ToString("MM/dd/yyyy HH:mm", CultureInfo.InvariantCulture);
                var filter = $"[Start] >= '{startStr}' AND [End] <= '{endStr}'";

                restricted = ditems.Restrict(filter);
                dynamic drest = restricted!;

                var q = (query ?? "").Trim();
                var result = new List<ApptDto>();

                foreach (var objItem in (System.Collections.IEnumerable)drest)
                {
                    if (result.Count >= max) { ReleaseCom(objItem); break; }

                    dynamic appt = objItem!;
                    try
                    {
                        string subj = SafeStr(() => (string)appt.Subject) ?? "";
                        string loc = SafeStr(() => (string)appt.Location) ?? "";
                        string bdy = SafeStr(() => (string)appt.Body) ?? "";

                        bool hit = q.Length == 0 || ContainsCI(subj, q) || ContainsCI(loc, q) || ContainsCI(bdy, q);
                        if (!hit) continue;

                        string entryId = SafeStr(() => (string)appt.EntryID) ?? "";
                        DateTime s = (DateTime)appt.Start;
                        DateTime e = (DateTime)appt.End;
                        bool allDay = SafeBool(() => (bool)appt.AllDayEvent);

                        result.Add(new ApptDto(entryId, subj, loc, s, e, allDay));
                    }
                    finally { ReleaseCom(objItem); }
                }

                return ToJson(result);
            }
            finally
            {
                ReleaseCom(restricted);
                ReleaseCom(items);
                ReleaseCom(folder);
                ReleaseCom(ns);
                ReleaseCom(app);
            }
        });

    public static string GetCalendarItemByEntryId(string entryId, string? storeName)
        => StaThread.Run(() =>
        {
            if (string.IsNullOrWhiteSpace(entryId))
                throw new ArgumentException("entry_id ist leer.");

            object? app = null;
            object? ns = null;
            object? itemObj = null;
            object? storeObj = null;

            try
            {
                (app, ns) = GetOutlook();
                dynamic dns = ns!;

                if (!string.IsNullOrWhiteSpace(storeName))
                {
                    storeObj = FindStoreObject(dns, storeName!);
                    dynamic store = storeObj;
                    string storeId = SafeStr(() => (string)store.StoreID) ?? "";
                    itemObj = dns.GetItemFromID(entryId, storeId);
                }
                else
                {
                    itemObj = dns.GetItemFromID(entryId);
                }

                if (itemObj == null) throw new InvalidOperationException("Item nicht gefunden.");

                dynamic appt = itemObj;
                string subj = SafeStr(() => (string)appt.Subject) ?? "";
                string loc = SafeStr(() => (string)appt.Location) ?? "";
                DateTime s = (DateTime)appt.Start;
                DateTime e = (DateTime)appt.End;
                bool allDay = SafeBool(() => (bool)appt.AllDayEvent);

                return ToJson(new ApptDto(entryId, subj, loc, s, e, allDay));
            }
            finally
            {
                ReleaseCom(itemObj);
                ReleaseCom(storeObj);
                ReleaseCom(ns);
                ReleaseCom(app);
            }
        });

    // ---------------- internals ----------------

    private static (object app, object ns) GetOutlook()
    {
        object appObj;

        if (ComRuntime.TryGetActiveObject("Outlook.Application", out var active) && active != null)
            appObj = active;
        else
        {
            var t = Type.GetTypeFromProgID("Outlook.Application", throwOnError: true)!;
            appObj = Activator.CreateInstance(t)!;
        }

        dynamic app = appObj;
        object nsObj = app.GetNamespace("MAPI");
        try { ((dynamic)nsObj).Logon("", "", false, false); } catch { }

        return (appObj, nsObj);
    }

    private static object FindStoreObject(dynamic ns, string storeName)
    {
        foreach (var rootObj in (System.Collections.IEnumerable)ns.Folders)
        {
            dynamic root = rootObj!;
            try
            {
                string name = SafeStr(() => (string)root.Name) ?? "";
                if (string.Equals(name, storeName, StringComparison.OrdinalIgnoreCase))
                {
                    // root.Store ist das Store-Objekt
                    return (object)root.Store;
                }
            }
            finally
            {
                ReleaseCom(rootObj);
            }
        }
        throw new InvalidOperationException($"Store nicht gefunden: \"{storeName}\" (Tipp: outlook_list_stores)." );
    }

    private static object ResolveFolderByStoreAndSpec(object nsObj, string storeName, string folderSpec)
    {
        dynamic ns = nsObj;

        // Normalize
        folderSpec = (folderSpec ?? "{inbox}").Trim();
        if (folderSpec.Length == 0) folderSpec = "{inbox}";

        // Absolute path accidentally passed? e.g. "store\\Inbox\\Sub"
        // If starts with storeName\\, strip it.
        var fsNorm = NormalizePath(folderSpec);
        if (fsNorm.StartsWith(storeName + "\\", StringComparison.OrdinalIgnoreCase))
            fsNorm = fsNorm.Substring(storeName.Length + 1);

        // split
        var parts = fsNorm.Split('\\', StringSplitOptions.RemoveEmptyEntries);

        // Start folder
        object currentObj;

        if (parts.Length == 0 || IsRootToken(fsNorm))
        {
            currentObj = FindStoreRoot(ns, storeName);
            return currentObj;
        }

        // token or localized alias as first segment?
        if (TryMapDefaultFolder(parts[0], out var defFolderId))
        {
            currentObj = GetDefaultFolder(ns, storeName, defFolderId);
            // Walk remaining segments under that folder
            for (int i = 1; i < parts.Length; i++)
            {
                currentObj = WalkToChild(currentObj, parts[i], $"{storeName}\\{parts[0]}");
            }
            return currentObj;
        }

        // Otherwise: treat as relative under store root
        currentObj = FindStoreRoot(ns, storeName);
        for (int i = 0; i < parts.Length; i++)
        {
            currentObj = WalkToChild(currentObj, parts[i], $"{storeName}\\{string.Join("\\", parts.Take(i))}");
        }
        return currentObj;
    }

    private static object FindStoreRoot(dynamic ns, string storeName)
    {
        foreach (var rootObj in (System.Collections.IEnumerable)ns.Folders)
        {
            dynamic root = rootObj!;
            try
            {
                string name = SafeStr(() => (string)root.Name) ?? "";
                if (string.Equals(name, storeName, StringComparison.OrdinalIgnoreCase))
                    return rootObj!;
            }
            catch
            {
                ReleaseCom(rootObj);
                throw;
            }

            ReleaseCom(rootObj);
        }

        throw new InvalidOperationException($"Store nicht gefunden: \"{storeName}\" (Tipp: outlook_list_stores)." );
    }

    private static object GetDefaultFolder(dynamic ns, string storeName, int defFolderId)
    {
        object rootObj = FindStoreRoot(ns, storeName);
        dynamic root = rootObj;
        object storeObj = root.Store;
        dynamic store = storeObj;

        try
        {
            object folder = store.GetDefaultFolder(defFolderId);
            return folder;
        }
        finally
        {
            ReleaseCom(storeObj);
            ReleaseCom(rootObj);
        }
    }

    private static object WalkToChild(object currentObj, string childName, string prefix)
    {
        dynamic current = currentObj;

        foreach (var fObj in (System.Collections.IEnumerable)current.Folders)
        {
            bool isMatch = false;
            dynamic f = fObj!;
            try
            {
                string name = SafeStr(() => (string)f.Name) ?? "";
                if (string.Equals(name, childName, StringComparison.OrdinalIgnoreCase))
                {
                    isMatch = true;
                    // release parent
                    ReleaseCom(currentObj);
                    return fObj!; // DO NOT ReleaseCom(fObj) on match
                }
            }
            finally
            {
                // release only non-matching
                if (!isMatch)
                    ReleaseCom(fObj);
            }
        }

        ReleaseCom(currentObj);
        throw new InvalidOperationException($"Ordner nicht gefunden: \"{childName}\" in \"{prefix}\"" );
    }

    private static MailDto MapMail(dynamic item, bool includeBody, int bodyMaxChars, bool includeHeaders, int headersMaxChars)
    {
        string subject = SafeStr(() => (string)item.Subject) ?? "";
        string entryId = SafeStr(() => (string)item.EntryID) ?? "";
        string senderName = SafeStr(() => (string)item.SenderName) ?? "";
        string senderEmail = SafeGetSenderEmail(item);
        string to = SafeStr(() => (string)item.To) ?? "";
        string cc = SafeStr(() => (string)item.CC) ?? "";
        DateTime? received = SafeDate(() => (DateTime)item.ReceivedTime);
        bool unread = SafeBool(() => (bool)item.UnRead);
        int size = SafeInt(() => (int)item.Size);

        string? body = null;
        if (includeBody)
        {
            body = SafeStr(() => (string)item.Body) ?? "";
            if (body.Length > bodyMaxChars)
                body = body[..bodyMaxChars] + "\n...[gekürzt]";
        }

        string? headers = null;
        if (includeHeaders)
        {
            headers = SafeStr(() => (string)item.InternetMessageHeaders) ?? "";
            if (headers.Length > headersMaxChars)
                headers = headers[..headersMaxChars] + "\n...[gekürzt]";
        }

        return new MailDto(entryId, subject, senderName, senderEmail, to, cc, received, unread, size, body, headers);
    }

    private static bool TryMapDefaultFolder(string tokenOrName, out int folderId)
    {
        folderId = 0;
        var t = NormalizeToken(tokenOrName);

        // accept tokens + localized names
        return t switch
        {
            "inbox" or "posteingang" => (folderId = OlFolder_Inbox) != 0,
            "sent" or "sentitems" or "gesendet" or "gesendeteelemente" or "gesendete elemente" => (folderId = OlFolder_Sent) != 0,
            "drafts" or "entwurfe" or "entwürfe" => (folderId = OlFolder_Drafts) != 0,
            "junk" or "junkemail" or "junk-e-mail" or "junk e-mail" => (folderId = OlFolder_Junk) != 0,
            "deleted" or "deleteditems" or "geloescht" or "gelöscht" or "geloeschteelemente" or "gelöschte elemente" => (folderId = OlFolder_Deleted) != 0,
            "calendar" or "kalender" => (folderId = OlFolder_Calendar) != 0,
            "root" => false,
            _ => false
        };
    }

    private static bool IsRootToken(string folderSpec)
    {
        var t = NormalizeToken(folderSpec);
        return t is "root" or "{root}";
    }

    private static string NormalizeFolderSpec(string folderSpec)
    {
        folderSpec = (folderSpec ?? "").Trim();
        if (folderSpec.Length == 0) return "{root}";
        return folderSpec;
    }

    private static string NormalizeToken(string token)
    {
        token = (token ?? "").Trim();

        if (token.StartsWith("{") && token.EndsWith("}"))
            token = token[1..^1];

        token = token.Replace("/", "\\");
        token = token.Trim().ToLowerInvariant();
        return token;
    }

    private static string SafeGetSenderEmail(dynamic mail)
    {
        try
        {
            string s = SafeStr(() => (string)mail.SenderEmailAddress) ?? "";
            if (!string.IsNullOrWhiteSpace(s)) return s;
        }
        catch { }

        try
        {
            const string PR_SENDER_SMTP_ADDRESS = "http://schemas.microsoft.com/mapi/proptag/0x5D01001F";
            object paObj = mail.PropertyAccessor;
            dynamic pa = paObj;
            try
            {
                object v = pa.GetProperty(PR_SENDER_SMTP_ADDRESS);
                return v?.ToString() ?? "";
            }
            finally { ReleaseCom(paObj); }
        }
        catch
        {
            return "";
        }
    }

    private static DateTime? ParseIsoOrNull(string? s)
    {
        if (string.IsNullOrWhiteSpace(s)) return null;
        if (DateTime.TryParse(s, CultureInfo.InvariantCulture, DateTimeStyles.AssumeLocal, out var dt))
            return dt;
        return null;
    }

    private static bool ContainsCI(string haystack, string needle)
        => needle.Length == 0 || (haystack ?? "").IndexOf(needle, StringComparison.OrdinalIgnoreCase) >= 0;

    private static string NormalizePath(string path) => (path ?? "").Trim().Replace("/", "\\");

    private static string? SafeStr(Func<string> getter) { try { return getter(); } catch { return null; } }
    private static int SafeInt(Func<int> getter) { try { return getter(); } catch { return 0; } }
    private static bool SafeBool(Func<bool> getter) { try { return getter(); } catch { return false; } }
    private static DateTime? SafeDate(Func<DateTime> getter) { try { return getter(); } catch { return null; } }

    private static void ReleaseCom(object? o)
    {
        try
        {
            if (o != null && Marshal.IsComObject(o))
                Marshal.ReleaseComObject(o);
        }
        catch { }
    }

    private static string ToJson<T>(T value)
        => JsonSerializer.Serialize(value, new JsonSerializerOptions { WriteIndented = true });
}
