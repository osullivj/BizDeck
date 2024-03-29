package ;

import haxe.ui.HaxeUIApp;
import haxe.ui.Toolkit;
import js.Browser;

class Main {
    public static function main() {
		// set the top level container to be main-view.xml:bd_top_vbox
		Toolkit.init({
			container: js.Browser.document.getElementById("bd_top_vbox")
		});
        var app = new HaxeUIApp();
        app.ready(function() {
            app.addComponent(new MainView());
            app.start();
        });
    }
}
