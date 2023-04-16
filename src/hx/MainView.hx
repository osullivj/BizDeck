package ;

import haxe.ui.containers.VBox;
import haxe.ui.containers.Box;
import haxe.ui.containers.TableView;
import haxe.ui.events.MouseEvent;
import js.html.WebSocket;
import js.Browser;


class BizDeckWebSocket {
	public var websock:WebSocket;
	public var connected:Bool;
	public var mainview:MainView;
	
	public function new(mv:MainView) {
		this.mainview = mv;
		this.connected = false;
		this.websock = new WebSocket(mv.websock_url, "json");
		this.websock.onopen = function() {
			trace("ws.conn");
		};	
		this.websock.onmessage = function(e) {
			trace("ws.recv: " + e.data);
			var obj = haxe.Json.parse(e.data);
			switch obj.Type {
				case "connected": this.connected = true;
				case "config": this.mainview.on_config(obj.Data);
			}
		};
		this.websock.onclose = function() {
			trace("ws.disconn");
			this.connected = false;
			this.websock = null;
		};
	}
}

@:build(haxe.ui.ComponentBuilder.build("main-view.xml"))
class MainView extends VBox {
	var websock:BizDeckWebSocket;
	public var websock_url:String;
	
    public function new() {
        super();
		var port = js.Browser.document.location.port;
		this.websock_url = "ws://localhost:" + port + "/ws";
		this.websock = new BizDeckWebSocket(this);
	}
	
	public function on_config(config:Dynamic) {
		var tv:TableView = this.findComponent("bd_config_tableview");
		tv.clearContents(true);
		var cfg:haxe.DynamicAccess<Dynamic> = config;
		for (key in cfg.keys()) {
			var val:Any = cfg.get(key);
			if (key == "BizDeckConfig") {
				var subcfg:haxe.DynamicAccess<Dynamic> = val;
				for (subkey in subcfg.keys()) {
					var subval:String = subcfg.get(subkey);
					
				}
			}
		}
	}
}