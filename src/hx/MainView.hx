package ;

import haxe.Serializer;
import haxe.ui.containers.VBox;
import haxe.ui.containers.Box;
import haxe.ui.containers.TableView;
import haxe.ui.containers.TabView;
import haxe.ui.containers.dialogs.Dialog.DialogEvent;
import haxe.ui.components.Image;
import haxe.ui.data.ArrayDataSource;
import haxe.ui.events.MouseEvent;
import haxe.ui.events.UIEvent;
import js.html.WebSocket;
import js.Browser;
import DialogView;


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
				case "connected":
					this.connected = true;
				case "config": {
					this.mainview.on_config(obj.Data);
				}
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
	private var del_buttons_data_source:ArrayDataSource<Dynamic>;	
	private var config:haxe.DynamicAccess<Dynamic>;
	
    public function new() {
        super();
		// API has rendered in the browser, so open a client
		// websock back to the embedio server
		var port = js.Browser.document.location.port;
		this.websock_url = "ws://localhost:" + port + "/ws";
		this.websock = new BizDeckWebSocket(this);
		this.config = null;
	}
	
	public function on_button_add_button(e) {
		trace("Add clicked!");
	}

	public function on_button_del_button(e) {
		trace("Del clicked!");
	    var dialog = new BizDeckDeleteButtonDialog(this.del_buttons_data_source);
        dialog.onDialogClosed = function(e:DialogEvent) {
		    trace("on_button_del_button: button:" + e.button);
			if (e.button == "{{apply}}") {
				var del_msg = {
					type: "del_button",
					data: dialog.list_view.selectedItem.bd_del_btn_name
				};
				var del_msg_json = haxe.Json.stringify(del_msg);
				trace("on_button_del_button: send:" + del_msg_json);
				this.websock.websock.send(del_msg_json);
			}
        };
        dialog.showDialog();
	}

	public function on_config(cfg:Dynamic) {
		this.config = cfg;
		// Add button handlers that raise add and del dialogs
		var add_button = this.findComponent("bd_add_btn_btn");
		var del_button = this.findComponent("bd_del_btn_btn");
		add_button.onClick = this.on_button_add_button;
		del_button.onClick = this.on_button_del_button;
		
		// yes, we're splitting the contents of config.json
		// across two TableViews. Refactor needed....
		var cfg_tv:TableView = this.findComponent("bd_config_tableview");
		var btn_tv:TableView = this.findComponent("bd_buttons_tableview");
		
		// Build data sources for two tabs in tabview...
		config_data_source = new ArrayDataSource<Dynamic>();
		buttons_data_source = new ArrayDataSource<Dynamic>();
		// ...and data source for del button listview.
		del_buttons_data_source = new ArrayDataSource<Dynamic>();
		
		// clear table contents: false means don't clear headers
		cfg_tv.clearContents(false);
		btn_tv.clearContents(false);
		for (key in config.keys()) {
			var val:Any = config.get(key);
			if (key == "BizDeckConfig") {
				var subcfg:haxe.DynamicAccess<Dynamic> = val;
				for (subkey in subcfg.keys()) {
					var subval:Any = subcfg.get(subkey);
					if (subkey=="ButtonMap") {
						var button_list:haxe.DynamicAccess<Dynamic> = subval;
						for (btn_defn in button_list) {
							var btn_row:Any = {bd_btns_index:btn_defn.ButtonIndex,
										bd_btns_name:btn_defn.Name,
										bd_btns_type:btn_defn.Action,
										bd_btns_icon:btn_defn.ButtonImagePath};
							buttons_data_source.add(btn_row);
							if (btn_defn.Action != "native") {
								var del_btn_row:Any = {bd_del_btn_name:btn_defn.Name,
													bd_del_btn_type:btn_defn.Action,
													bd_del_btn_icon:btn_defn.ButtonImagePath};
								del_buttons_data_source.add(del_btn_row);
							}
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