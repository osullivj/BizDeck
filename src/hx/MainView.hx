package ;

import haxe.ui.containers.VBox;
import haxe.ui.containers.Box;
import haxe.ui.containers.TableView;
import haxe.ui.containers.TabView;
import haxe.ui.data.ArrayDataSource;
import haxe.ui.events.MouseEvent;
import haxe.ui.events.UIEvent;
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
	private var config_data_source:ArrayDataSource<Dynamic>;
	private var buttons_data_source:ArrayDataSource<Dynamic>;
	
    public function new() {
        super();
		// API has rendered in the browser, so open a client
		// websock back to the embedio server
		var port = js.Browser.document.location.port;
		this.websock_url = "ws://localhost:" + port + "/ws";
		this.websock = new BizDeckWebSocket(this);
	}
	
	public function on_config(config:Dynamic) {
		// yes, we're splitting the contents of config.json
		// across two TableViews. Refactor needed....
		var cfg_tv:TableView = this.findComponent("bd_config_tableview");
		var btn_tv:TableView = this.findComponent("bd_buttons_tableview");
		config_data_source = new ArrayDataSource<Dynamic>();
		buttons_data_source = new ArrayDataSource<Dynamic>();		
		// clear table contents: false means don't clear headers
		cfg_tv.clearContents(false);
		btn_tv.clearContents(false);
		var cfg:haxe.DynamicAccess<Dynamic> = config;
		for (key in cfg.keys()) {
			var val:Any = cfg.get(key);
			if (key == "BizDeckConfig") {
				var subcfg:haxe.DynamicAccess<Dynamic> = val;
				for (subkey in subcfg.keys()) {
					var subval:Any = subcfg.get(subkey);
					if (subkey=="ButtonMap") {
						var button_list:haxe.DynamicAccess<Dynamic> = subval;
						for (btn_defn in button_list) {
							var btn_row:Any = {bd_index:btn_defn.ButtonIndex,
										bd_name:btn_defn.Name,
										bd_icon:btn_defn.ButtonImagePath};
							buttons_data_source.add(btn_row);
							trace("mv.on_config: btn_row=" + btn_row);
						}
					}
					else {
						config_data_source.add({bd_setting:key+"."+subkey, bd_value:subval});
					}
				}
			}
			else {
				// NB setting and value in the jobj match the
				// element IDs in main-view.xml
				var cfg_row:Any = {bd_setting:key, bd_value:val};
				trace("mv.on_config: cfg_row=" + cfg_row);
				config_data_source.add(cfg_row);
			}
		}
		cfg_tv.dataSource = config_data_source;
		btn_tv.dataSource = buttons_data_source;
	}
}