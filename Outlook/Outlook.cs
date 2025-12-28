using ScriptStack.Runtime;
using System.Collections.ObjectModel;

namespace Outlook;

/// <summary>
/// ScriptStack Plugin: Outlook (COM Interop via dynamic)
///
/// Routinen (identisch zu deinem Outlook-Interop ToolRunner Plugin):
/// - outlook_list_stores
/// - outlook_get_default_store
/// - outlook_list_folders2
/// - outlook_list_mails
/// - outlook_get_mail
/// - outlook_search_mails2
/// - outlook_create_draft2
/// - outlook_search_calendar2
/// - outlook_get_calendar_item
///
/// Rückgabewert ist jeweils ein JSON-String.
/// </summary>
public sealed class Outlook : Model
{

    private static ReadOnlyCollection<Routine>? exportedRoutines;

    public Outlook()
    {

        if (exportedRoutines != null) 
            return;

        List<Routine> routines = new List<Routine>();

        routines.Add(new Routine((Type?)null, "outlook_list_stores"));
        routines.Add(new Routine((Type?)null, "outlook_get_default_store"));

        // store_name, folderSpec
        routines.Add(new Routine((Type)null, "outlook_list_folders2", typeof(string), typeof(string)));

        // store_name, folderSpec, skip, max, include_body, body_max_chars
        List<Type> outlook_list_mails_types = new List<Type>();
        outlook_list_mails_types.Add(typeof(string));
        outlook_list_mails_types.Add(typeof(string));
        outlook_list_mails_types.Add(typeof(int));
        outlook_list_mails_types.Add(typeof(int));
        outlook_list_mails_types.Add(typeof(bool));
        outlook_list_mails_types.Add(typeof(int));
        routines.Add(new Routine((Type?)null, "outlook_list_mails", outlook_list_mails_types));

        // entry_id, store_name?, include_body, body_max_chars, include_headers, headers_max_chars
        List<Type> outlook_get_mail_types = new List<Type>();
        outlook_get_mail_types.Add(typeof(string));
        outlook_get_mail_types.Add(typeof(string));
        outlook_get_mail_types.Add(typeof(bool));
        outlook_get_mail_types.Add(typeof(int));
        outlook_get_mail_types.Add(typeof(bool));
        outlook_get_mail_types.Add(typeof(int));
        routines.Add(new Routine((Type?)null, "outlook_get_mail", outlook_get_mail_types));

        // store_name, save_folder, to, cc, bcc, subject, body, is_html
        List<Type> outlook_create_draft2_types = new List<Type>();
        outlook_create_draft2_types.Add(typeof(string));
        outlook_create_draft2_types.Add(typeof(string));
        outlook_create_draft2_types.Add(typeof(string));
        outlook_create_draft2_types.Add(typeof(string));
        outlook_create_draft2_types.Add(typeof(string));
        outlook_create_draft2_types.Add(typeof(string));
        outlook_create_draft2_types.Add(typeof(string));
        outlook_create_draft2_types.Add(typeof(bool));
        routines.Add(new Routine((Type?)null, "outlook_create_draft2", outlook_create_draft2_types));

        // store_name, folderSpec, startIso, endIso, query, max
        List<Type> outlook_search_calendar_types = new List<Type>();
        outlook_search_calendar_types.Add(typeof(string));
        outlook_search_calendar_types.Add(typeof(string));
        outlook_search_calendar_types.Add(typeof(string));
        outlook_search_calendar_types.Add(typeof(string));
        outlook_search_calendar_types.Add(typeof(int));
        routines.Add(new Routine((Type?)null, "outlook_search_calendar2", outlook_search_calendar_types));

        // store_name, folderSpec, query, subject_contains, sender_contains, to_or_cc_contains, unread_only, received_after_iso, received_before_iso, max, include_body, body_max_chars
        List<Type> outlook_search_mails2_types = new List<Type>();
        outlook_search_mails2_types.Add(typeof(string));
        outlook_search_mails2_types.Add(typeof(string));
        outlook_search_mails2_types.Add(typeof(string));
        outlook_search_mails2_types.Add(typeof(string));
        outlook_search_mails2_types.Add(typeof(string));
        outlook_search_mails2_types.Add(typeof(string));
        outlook_search_mails2_types.Add(typeof(bool));
        outlook_search_mails2_types.Add(typeof(string));
        outlook_search_mails2_types.Add(typeof(string));
        outlook_search_mails2_types.Add(typeof(int));
        outlook_search_mails2_types.Add(typeof(bool));
        outlook_search_mails2_types.Add(typeof(int));
        routines.Add(new Routine((Type?)null, "outlook_search_mails2", outlook_search_mails2_types));

        // entry_id, store_name?
        routines.Add(new Routine((Type?)null, "outlook_get_calendar_item", typeof(string), typeof(string)));
        

        exportedRoutines = routines.AsReadOnly();

    }

    public ReadOnlyCollection<Routine> Routines => exportedRoutines!;

    public object? Invoke(string routine, List<object> parameters)
    {
        // NOTE: ScriptStack übergibt Parameter positional.
        // Wir machen hier defensive Defaults, damit optionale Parameter auch weggelassen werden können.

        return routine switch
        {
            "outlook_list_stores" => OutlookInterop.ListStores(),
            "outlook_get_default_store" => OutlookInterop.GetDefaultStore(),

            "outlook_list_folders2" => OutlookInterop.ListFolders2(
                GetString(parameters, 0, required: true)!,
                GetString(parameters, 1, defaultValue: "{root}")!),

            "outlook_list_mails" => OutlookInterop.ListMails(
                GetString(parameters, 0, required: true)!,
                GetString(parameters, 1, defaultValue: "{inbox}")!,
                GetInt(parameters, 2, 0),
                GetInt(parameters, 3, 20),
                GetBool(parameters, 4, false),
                GetInt(parameters, 5, 2000)),

            "outlook_get_mail" => OutlookInterop.GetMailByEntryId(
                GetString(parameters, 0, required: true)!,
                GetString(parameters, 1, defaultValue: "") is { Length: > 0 } s ? s : null,
                GetBool(parameters, 2, true),
                GetInt(parameters, 3, 5000),
                GetBool(parameters, 4, false),
                GetInt(parameters, 5, 8000)),

            "outlook_search_mails2" => OutlookInterop.SearchMails2(
                GetString(parameters, 0, required: true)!,
                GetString(parameters, 1, defaultValue: "{inbox}")!,
                GetString(parameters, 2, defaultValue: "")!,
                GetString(parameters, 3, defaultValue: "")!,
                GetString(parameters, 4, defaultValue: "")!,
                GetString(parameters, 5, defaultValue: "")!,
                GetBool(parameters, 6, false),
                GetString(parameters, 7, defaultValue: "") is { Length: > 0 } a ? a : null,
                GetString(parameters, 8, defaultValue: "") is { Length: > 0 } b ? b : null,
                GetInt(parameters, 9, 50),
                GetBool(parameters, 10, false),
                GetInt(parameters, 11, 1000)),

            "outlook_create_draft2" => OutlookInterop.CreateDraft2(
                GetString(parameters, 0, required: true)!,
                GetString(parameters, 1, defaultValue: "{drafts}")!,
                GetString(parameters, 2, defaultValue: "")!,
                GetString(parameters, 3, defaultValue: "")!,
                GetString(parameters, 4, defaultValue: "")!,
                GetString(parameters, 5, defaultValue: "")!,
                GetString(parameters, 6, defaultValue: "")!,
                GetBool(parameters, 7, false)),

            "outlook_search_calendar2" => OutlookInterop.SearchCalendar2(
                GetString(parameters, 0, required: true)!,
                GetString(parameters, 1, defaultValue: "{calendar}")!,
                GetString(parameters, 2, required: true)!,
                GetString(parameters, 3, required: true)!,
                GetString(parameters, 4, defaultValue: "")!,
                GetInt(parameters, 5, 50)),

            "outlook_get_calendar_item" => OutlookInterop.GetCalendarItemByEntryId(
                GetString(parameters, 0, required: true)!,
                GetString(parameters, 1, defaultValue: "") is { Length: > 0 } s2 ? s2 : null),

            _ => throw new InvalidOperationException($"Unknown routine: {routine}")
        };
    }

    private static string? GetString(List<object> p, int idx, string? defaultValue = null, bool required = false)
    {
        if (idx >= p.Count || p[idx] is null)
        {
            if (required) throw new ArgumentException($"Missing parameter #{idx} (string)");
            return defaultValue;
        }

        if (p[idx] is string s) return s;
        return p[idx].ToString() ?? defaultValue;
    }

    private static int GetInt(List<object> p, int idx, int defaultValue)
    {
        if (idx >= p.Count || p[idx] is null) return defaultValue;
        if (p[idx] is int i) return i;
        if (int.TryParse(p[idx].ToString(), out var v)) return v;
        return defaultValue;
    }

    private static bool GetBool(List<object> p, int idx, bool defaultValue)
    {
        if (idx >= p.Count || p[idx] is null) return defaultValue;
        if (p[idx] is bool b) return b;
        if (bool.TryParse(p[idx].ToString(), out var v)) return v;
        return defaultValue;
    }
}
