```Javascript
// A simple HTTP GET request
function httpGET() {

	var req = http_request();
	req.Method = "GET";
	req.Url = "https://www.orf.at";
	req.ContentType = "text/html; charset=utf-8";
	req.Body = "";
	req.Headers = { "Authorization": "Bearer abcdef" };

	var resp = http_send(req);
	print(resp.Status);
	print(resp.Body);

}

// Download a file
function downloadFile() {

	var req = http_request();
	req.Method = "GET";
	req.Url = "https://example.com/image.jpeg";
	req.ReadResponseAsBytes = true;

	var resp = http_send(req);

	var f = fopen("C:\\image.jpeg", "wb");
	fwritebytes(f, resp.Bytes);
	fclose(f);
	
}
```

OpenAI API Test
```Javascript
var args;
var history, httpstream, tools, callId;

function execute_cmd_command(v) {
	var out = std.popen("cmd.exe", "/c " + json.get(v, "command"));
	return out["stdout"];
}

function dispatch(funcName, funcParams) {
	
	switch(funcName) {
		
		case "execute_cmd_command":
			return execute_cmd_command(funcParams);
	}
	
}

function main() {
	
	std.print(simple(args[1]));
	return;
	
	//stream(args[1]);
	//return;
	
	history = [];
	var question;
	while(true) 
	{
		
		std.print("> ");
		question = std.readLine();
		
		if(question == "exit")
			break;
		
		if(std.startsWith(question, "chat")) {
			std.replace(question, "chat", "");
			var response = stream_with_tools(question);			
			while(response == true) 
			{
				response = stream_with_tools("");
			}
			continue;
		}
		
		if(question != "") {
			var cmd = std.popen("cmd.exe", "/c " + question);
			if(null != cmd) {
				if(null != cmd["stdout"])
					std.print(cmd["stdout"].Trim() + "\n");
				else
					std.print(cmd["stderr"].Trim() + "\n");
			}
		}
		
	}
	
	if(history.size > 0)
		std.print(json.parse(history.ToString()));
	
}

function simple(q) {
	
	// create a new http request
	var req = http.request();
	req.Method = "POST";
	req.Url = "https://api.openai.com/v1/responses"
	req.ContentType = "application/json; charset=utf-8";
	req.Headers = { "Authorization": "Bearer " + std.getenv("OPENAI_API_KEY") };
	
	// set body /w tools
	var body = { 
		"model": "gpt-5-nano", 
		"input": [
			{ "role": "system", "content": "Du bist ein hilfreicher Chatbot" },
			{ "role": "user", "content": q }
		] 
	};
	req.Body = body.ToString();
	
	var res = http.send(req);	
	
	return json.get(json.parse(res.Body), "output[1].content[0].text");
	
}

function stream(q) {

	// create a new http request
	var req = http.request();
	req.Method = "POST";
	req.Url = "https://api.openai.com/v1/responses"
	req.ContentType = "application/json; charset=utf-8";
	req.Headers = { "Authorization": "Bearer " + std.getenv("OPENAI_API_KEY") };

	// set body /w tools
	var body = { 
		"model": "gpt-5-nano", 
		"stream": true,
		"input": [
			{ "role": "system", "content": "Du bist ein hilfreicher Chatbot" },
			{ "role": "user", "content": q }
		]
	};
	req.Body = body.ToString();

	// get response with sse
	httpstream = http.open_stream(req);	
	var line = "";
	
	while(true) 
	{
		
		// get the next line
		line = http.stream_read_line(httpstream);	
		
		// if there is no data
		if(line == null) {
			std.print("\n");
			break;  // we're done
		}
		
		// if line is empty, model is still working, so we keep waiting
		if(line == "")
			continue;		
		
		// if line has data attribute
		if(std.match(line, "data: ")) 
		{	
			
			// parse the json output
			var j = json.parse(std.replace(line, "data: ", ""));

			// if delta element was found, we got a streamed part of the text response
			if(json.get(j, "type") == "response.output_text.delta") 
			{
				// print it in realtime
				std.print( json.get(j, "delta") );
			}

		}
		
	}

}

function stream_with_tools(q) {
	
	// create a new history item
	var item = {};
	item.role = "user";
	item.content = q;
	
	// if there was a question, add it, otherwise its a follow up tool call	
	if(q != "")
		history[history.size] = item;

	// create a new http request
	var req = http.request();
	req.Method = "POST";
	req.Url = "https://api.openai.com/v1/responses"
	req.ContentType = "application/json; charset=utf-8";
	req.Headers = { "Authorization": "Bearer " + std.getenv("OPENAI_API_KEY") };
	
	// define tools
	tools = [
    {
        "type":"function",
        "name":"execute_cmd_command",
        "description":"Führe einen Befehl in Windows CMD aus und erhalte die Ausgabe als String.",
        "parameters":{
            "type":"object",
            "properties":{
                "command":{
                    "type":"string"
                }
            },
            "required":[
                "command"
            ],
            "additionalProperties":false
        },
        "strict":true
    },
	{
		
	}];
	
	// set body /w tools
	var body = { 
		"model": "gpt-5-nano", 
		"stream": true,
		"tools": tools,
		"tool_choice": "auto",
		"input": history 
	};
	req.Body = body.ToString();

	// get response with sse
	httpstream = http.open_stream(req);	
	var line = "";
	var fullanswer = "";
	var hasToolCall = false;
	
	while(true) 
	{
		
		// get the next line
		line = http.stream_read_line(httpstream);	
		
		// if there is no data
		if(line == null) {
			std.print("\n");
			break;  // we're done
		}
		
		// if line is empty, model is still working, so we keep waiting
		if(line == "")
			continue;		
		
		// if line has data attribute
		if(std.match(line, "data: ")) 
		{	
			
			// parse the json output
			var j = json.parse(std.replace(line, "data: ", ""));

			// there can be more output items than one, we're only tracking tool calls for now
			if(json.get(j, "type") == "response.output_item.added") { }
			
			// if delta element was found, we got a streamed part of the text response
			if(json.get(j, "type") == "response.output_text.delta") 
			{
				// print it in realtime
				std.print( json.get(j, "delta") );
				// add it to the full answer so we can story it in history when finished
				fullanswer += "" + json.get(j, "delta");
			}
			
			// if an output item is done 
			if(json.get(j, "type") == "response.output_item.done") 
			{
				
				// if its a tool call
				if(json.get(j, "item.type") == "function_call") 
				{
					
					// and the output item is already completed
					// means we also got all the parameters
					if(json.get(j, "item.status") == "completed") 
					{
						
						// collect call_id, function name and parameters
						callId = json.get(j, "item.call_id");
						var funcName = json.get(j, "item.name");
						var funcParams = json.parse(json.get(j, "item.arguments"));	
						
						// just for debugging to know what gets called
						std.print("\n[TOOL CALL] " + funcName + " " + funcParams.ToString() + "\n");
						
						// add the tool call to the history
						history[history.size] = {
							"type": "function_call",
							"call_id": callId,
							"name": funcName,
							"arguments": funcParams.ToString()
						};
						
						// execute the tool call
						var ret = dispatch(funcName, funcParams);
						
						// and also add the result to the history
						history[history.size] = {
							"type": "function_call_output",
							"call_id": callId,
							"output": ""+ret
						};
						
						// we return true to indicate there was a tool call
						// so the full output is sent to the model again for evaluation
						hasToolCall = true;

					}
				
				}
				
			}
		
		}
		
	}
	
	// if there was a tool call we already added the results of the tool calls to history
	// so we return true to imply that a follow up turn should be made
	if(hasToolCall)
		return true;
	
	// if there was no tool call just add the answer to history and return false
	item = {};
	item.role = "system";
	item.content = fullanswer;	
	history[history.size] = item;
	
	return false;

}
```
