package ;

import haxe.ui.notifications.NotificationType;
import haxe.ui.notifications.NotificationManager;
import haxe.ui.containers.dialogs.CollapsibleDialog;
import haxe.ui.containers.ListView;
import haxe.ui.containers.dialogs.Dialog;
import haxe.ui.containers.dialogs.Dialogs;
import haxe.ui.containers.dialogs.MessageBox.MessageBoxType;
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
