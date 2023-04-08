package ;

import haxe.ui.containers.VBox;
import haxe.ui.events.MouseEvent;
import js.html.WebSocket;


class BizDeckWebSocket {
	public var ws:WebSocket;
	public function new(url:String) {
		this.ws = new WebSocket(url);
		this.ws.onopen = function() {
			trace("CONNECT");
			this.ws.send("TestString");
		};	
		this.ws.onmessage = function(e) {
			trace("RECEIVE: " + e.data);
		};
		this.ws.onclose = function() {
			trace("DISCONNECT");
		};
	}
}

@:build(haxe.ui.ComponentBuilder.build("main-view.xml"))
class MainView extends VBox {
    public function new() {
        super();
		var ws = new BizDeckWebSocket("ws://localhost:9271");
    }
}