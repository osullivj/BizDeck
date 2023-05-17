package ;

import haxe.ui.notifications.NotificationType;
import haxe.ui.notifications.NotificationManager;
import haxe.ui.containers.dialogs.CollapsibleDialog;
import haxe.ui.containers.ListView;
import haxe.ui.containers.dialogs.Dialog;
import haxe.ui.containers.dialogs.Dialogs.openTextFile;
import haxe.ui.containers.dialogs.Dialogs.SelectedFileInfo;
import haxe.ui.containers.dialogs.Dialogs.FileDialogTypes;
import haxe.ui.containers.dialogs.Dialogs.FileDialogExtensionInfo;
import haxe.ui.containers.dialogs.OpenFileDialog;
import haxe.ui.containers.dialogs.MessageBox.MessageBoxType;
import haxe.ui.components.Button;
import haxe.ui.components.TextField;
import haxe.ui.components.TextArea;
import haxe.ui.events.MouseEvent;
import haxe.ui.data.ArrayDataSource;
import haxe.ui.backend.OpenFileDialogBase.OpenFileDialogOptions;

class BizDeckFileDialogTypes extends FileDialogTypes {    
    public static var DOCS(get, null):Array<FileDialogExtensionInfo>;
    private static function get_DOCS():Array<FileDialogExtensionInfo> {
        return [{label: "Documents", extension: "doc, docx, xls, xlsx, txt, csv"}];
    }
    
    public static var EXES(get, null):Array<FileDialogExtensionInfo>;
    private static function get_EXES():Array<FileDialogExtensionInfo> {
        return [{label: "Executables", extension: "exe"}];
    }
    
    public static var JSON(get, null):Array<FileDialogExtensionInfo>;
    private static function get_JSON():Array<FileDialogExtensionInfo> {
        return [{label: "JSON", extension: "json, jsn"}];
    }
}

@:build(haxe.ui.macros.ComponentMacros.build("del-btn-dlg.xml"))
class BizDeckDeleteButtonDialog extends Dialog {
	public var list_view:ListView;
    public function new(ds:ArrayDataSource<Dynamic>) {
        super();
		// Connect the data source from MainView to 
		// out ListView
		list_view = this.findComponent("bd_del_btn_listview");
		list_view.dataSource = ds;
		trace("BizDeckDeleteButtonDialog: ds["+ds+"]");
		// Two std modal buttons
        buttons = DialogButton.CANCEL | DialogButton.APPLY;
    }
}

@:build(haxe.ui.macros.ComponentMacros.build("add-btn-dlg.xml"))
class BizDeckAddButtonDialog extends Dialog {
	// fields for "Add App"
	public var choose_btn:Button;
	public var script_name_text_field:TextField;
	public var script_text_area:TextArea;
	public var background_text_field:TextField;
	public var select_script_dialog_options:OpenFileDialogOptions;
	public var button_file_names:Array<String>;
	public var script_name_ok:Bool;

    public function new(bfn:Array<String>, status:haxe.DynamicAccess<Dynamic>) {
        super();
		button_file_names = bfn;
		script_name_ok = true;
		// hook up the fields in the dialog
		script_name_text_field = this.findComponent("bd_script_name_text_field");
		script_text_area = this.findComponent("bd_script_text_area");
		background_text_field = this.findComponent("bd_background_text_field");
		background_text_field.text = status.get("BackgroundDefault");
		// Cancel and OK modal buttons
        buttons = DialogButton.CANCEL | DialogButton.OK;
		select_script_dialog_options = {
            readContents: true,
            title: "Select JSON script",
            readAsBinary: false,
			multiple: false,
            extensions: BizDeckFileDialogTypes.JSON
		};
		// Add click handlers for the choose buttons
		choose_btn = this.findComponent("bd_choose_script_btn");
		choose_btn.onClick = function(e) {
			file_chooser_dialog(select_script_dialog_options, this.on_script_chosen);
		};
	}

	public function on_script_chosen(script){
		this.script_name_text_field.text = script.name;
		this.script_text_area.text = script.text;
		if (button_file_names.contains(script.name)) {
			NotificationManager.instance.addNotification({
				title:"Bad script name",
				body:script.name + " already exists. Please rename."
			});
			script_name_ok = false;
		}
	}

	public function file_chooser_dialog(options, callback) {
		var file_dialog = new OpenFileDialog();
		file_dialog.options = options;
		file_dialog.onDialogClosed = function(e) {
			if (file_dialog.selectedFiles != null) {
				if (file_dialog.selectedFiles.length > 0) {
					var selected_file = file_dialog.selectedFiles[0];
					trace("onDialogClose: file["+selected_file+"]");
					callback(selected_file);
				}
			}
		};
		file_dialog.show();
	}
}
