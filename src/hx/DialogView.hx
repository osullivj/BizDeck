package ;

import haxe.ui.notifications.NotificationType;
import haxe.ui.notifications.NotificationManager;
import haxe.ui.containers.dialogs.CollapsibleDialog;
import haxe.ui.containers.ListView;
import haxe.ui.containers.dialogs.Dialog;
import haxe.ui.containers.dialogs.MessageBox.MessageBoxType;
import haxe.ui.components.Button;
import haxe.ui.components.TextField;
import haxe.ui.events.MouseEvent;
import haxe.ui.data.ArrayDataSource;

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
	public var add_app_btn:Button;
	public var exe_path_textfield:TextField;
	public var exe_args_textfield:TextField;
	// fields for "Add Steps"
	public var add_steps_btn:Button;
	public var steps_path_textfield:TextField;
    public function new() {
        super();
		// Get hold of the Add buttons so we can attach onClick methods
		add_app_btn = this.findComponent("bd_add_app_btn");
		add_steps_btn = this.findComponent("bd_add_steps_btn");
		add_app_btn.onClick = function(e) { trace(e);}
		add_app_btn.onClick = function(e) { trace(e);}
		// hook up the fields in the dialog
		exe_path_textfield = this.findComponent("bd_exe_path_textfield");
		exe_args_textfield = this.findComponent("bd_exe_args_textfield");
		steps_path_textfield = this.findComponent("bd_steps_path_textfield");
		// Just one std model as we use the Add* buttons for APPLY/OK
        buttons = DialogButton.CANCEL;
    }
}
