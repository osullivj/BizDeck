package ;

import haxe.ui.containers.VBox;
import haxe.ui.containers.Box;
import haxe.ui.events.MouseEvent;
import js.html.WebSocket;
import js.Browser;


class BizDeckWebSocket {
	public var ws:WebSocket;
	public function new(url:String) {
		this.ws = new WebSocket(url, "json");
		this.ws.onopen = function() {
			trace("ws.conn");
		};	
		this.ws.onmessage = function(e) {
			trace("ws.recv: " + e.data);
			this.ws.send('{type:"hello",data:{}}');
		};
		this.ws.onclose = function() {
			trace("ws.disconn");
		};
	}
}

@:build(haxe.ui.ComponentBuilder.build("main-view.xml"))
class MainView extends VBox {
	var websock:BizDeckWebSocket;
	var websock_url:String;
    public function new() {
        super();
		var port = js.Browser.document.location.port;
		this.websock_url = "ws://localhost:" + port + "/ws";
		this.websock = new BizDeckWebSocket(this.websock_url);
	}
}