package ;

import haxe.ui.notifications.NotificationType;
import haxe.ui.notifications.NotificationManager;
import haxe.Serializer;
import haxe.ui.containers.VBox;
import haxe.ui.containers.Box;
import haxe.ui.containers.TableView;
import haxe.ui.containers.TreeView;
import haxe.ui.containers.TreeViewNode;
import haxe.ui.containers.TabView;
import haxe.ui.containers.dialogs.Dialog.DialogEvent;
import haxe.ui.components.Button;
import haxe.ui.components.Image;
import haxe.ui.components.Switch;
import haxe.ui.components.Slider;
import haxe.ui.components.TextField;
import haxe.ui.components.Label;
import haxe.ui.data.ArrayDataSource;
import haxe.ui.events.MouseEvent;
import haxe.ui.events.UIEvent;
import js.html.WebSocket;
import js.Browser;
import DialogView;
import Utilities.update_treeview;


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
			var obj = haxe.Json.parse(e.data);
   			trace("ws.recv: " + obj.type);
			switch obj.type {
				case "connected":
					this.connected = true;
				case "config":
					this.mainview.on_config(obj.data);
				case "status":
					this.mainview.on_status(obj.data);
				case "cache":
					this.mainview.on_cache(obj.data);
				case "notification":
					NotificationManager.instance.addNotification(obj.data);
                default:
                    trace("ws.recv: UNKNOWN type in: " + e.data);                    
			}
		};
		this.websock.onclose = function() {
			trace("ws.disconn");
			this.connected = false;
			this.websock = null;
		};
	}

	public function send_notification(notification:Dynamic) {
		trace("send_notification: " + notification);
	}

	public function send_message(mtype:String, mdata:Dynamic) {
		var msg = {type: mtype, data:mdata};
		var msg_json = haxe.Json.stringify(msg);
		if (connected) {
			trace("send_message: " + msg_json);
			websock.send(msg_json);
		}
		else {
			trace("send_message: disconnected, cannot send: " + msg_json);
		}
	}
}

@:build(haxe.ui.ComponentBuilder.build("main-view.xml"))
class MainView extends VBox {
	var websock:BizDeckWebSocket;
	public var websock_url:String;
	private var config_data_source:ArrayDataSource<Dynamic>;
	private var buttons_data_source:ArrayDataSource<Dynamic>;
	private var del_buttons_data_source:ArrayDataSource<Dynamic>;
	private var button_file_names:Array<String>;
	private var config:haxe.DynamicAccess<Dynamic>;
	private var status:haxe.DynamicAccess<Dynamic>;
	private var cache:haxe.DynamicAccess<Dynamic>;
	private var brightness_slider:Slider;
	private var brightness_save_button:Button;
	
    public function new() {
        super();
		// API has rendered in the browser, so open a client
		// websock back to the embedio server
		this.websock_url = "ws://" + js.Browser.document.location.host + "/ws";
		trace("MainView: websock_url: " + this.websock_url);
		this.websock = new BizDeckWebSocket(this);
		this.config = null;
		brightness_slider = this.findComponent("bd_brightness_slider");
		brightness_slider.onChange = this.on_brightness_slider_change;
		brightness_save_button = this.findComponent("bd_brightness_save_button");
		brightness_save_button.onClick = this.on_brightness_save_button_click;
	}

	private function on_brightness_save_button_click(e:UIEvent) {
		trace("on_brightness_save_button_click: e.type:" + e.type);
		this.websock.send_message("save_brightness", brightness_slider.pos);
	}

	private function on_brightness_slider_change(e:UIEvent) {
		trace("on_brightness_slider_change: e.type:" + e.type + ", slider.pos:" + brightness_slider.pos);
		// reject 0 brightness
		if (brightness_slider.pos == 0)
			return;
		this.websock.send_message("set_brightness", brightness_slider.pos);
	}
	
	public function on_button_add_button(e) {
		trace("on_button_add_button: e.type: " + e.type);
	    var dialog = new BizDeckAddButtonDialog(button_file_names, status);
        dialog.onDialogClosed = function(e:DialogEvent) {
		    trace("on_button_add_button: button:" + e.button);
			if (e.button == "{{ok}}") {
				var data = {
					name:dialog.script_name_text_field.text,
					json:dialog.script_text_area.text,
					background:dialog.background_text_field.text
				};
				this.websock.send_message("add_button", data);
			}
        };
        dialog.showDialog();
	}

	public function on_button_del_button(e) {
		trace("Del clicked!");
	    var dialog = new BizDeckDeleteButtonDialog(this.del_buttons_data_source);
        dialog.onDialogClosed = function(e:DialogEvent) {
		    trace("on_button_del_button: button:" + e.button);
			if (e.button == "{{apply}}") {
				this.websock.send_message("del_button", dialog.list_view.selectedItem.bd_del_btn_name);
			}
        };
        dialog.showDialog();
	}

	public function on_status(stat:Dynamic) {
		this.status = stat;
		var connected_checkbox:Switch = this.findComponent("bd_deck_connected_checkbox");
		var start_time_label:Label = this.findComponent("bd_start_time_label");
		var button_count_label:Label = this.findComponent("bd_button_count_label");
		var button_size_label:Label = this.findComponent("bd_button_size_label");
		var device_name_label:Label = this.findComponent("bd_device_name_label");
		var my_url_label:Label = this.findComponent("bd_my_url_label");
		for (key in status.keys()) {
			var val:Any = status.get(key);
			switch (key) {
				case "DeckConnection":
					connected_checkbox.selected = val;
				case "StartTime":
					start_time_label.htmlText = 'Started: ${val}';
				case "DeviceName":
					device_name_label.htmlText = 'Device: ${val}';
				case "ButtonCount":
					button_count_label.htmlText = 'Button count: ${val}';
				case "ButtonSize":
					button_size_label.htmlText = 'Button size: ${val}';
				case "MyURL":
					my_url_label.htmlText = 'My URL: ${val}';
				case "Brightness":
					brightness_slider.pos = val;
			}
		}
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
		button_file_names = new Array<String>();
		
		// Clear table contents: false means don't clear headers
		// Note that where JSON key names are quoted they must
		// match the property names in ConfigHelper.cs and
		// BizDeckConfig.cs
		cfg_tv.clearContents(false);
		btn_tv.clearContents(false);
		for (key in config.keys()) {
			var val:Any = config.get(key);
			if (key == "BizDeckConfig") {
				var subcfg:haxe.DynamicAccess<Dynamic> = val;
				for (subkey in subcfg.keys()) {
					var subval:Any = subcfg.get(subkey);
					if (subkey=="button_list") {
						var button_list:haxe.DynamicAccess<Dynamic> = subval;
						for (btn_defn in button_list) {
							var btn_row:Any = {bd_btns_name:btn_defn.name,
										bd_btns_type:btn_defn.action,
										bd_btns_icon:btn_defn.image};
							buttons_data_source.add(btn_row);
							button_file_names.push(btn_defn.name + ".json");
							if (btn_defn.action != "System") {
								var del_btn_row:Any = {bd_del_btn_name:btn_defn.name,
													bd_del_btn_type:btn_defn.action,
													bd_del_btn_icon:btn_defn.image};
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

	public function on_cache(cch:Dynamic) {
		this.cache = cch;
		// Get a handle on the TreeView
		var cache_treeview:TreeView = this.findComponent("bd_cache_treeview");
		cache_treeview.clearNodes();
		update_treeview(cache_treeview, this.cache);
	}
}