package ;

import haxe.DynamicAccess;
import haxe.Serializer;
import haxe.ui.containers.TreeView;
import haxe.ui.containers.TreeViewNode;
import haxe.ui.data.ArrayDataSource;

// Assumptions
// Only one atomic: String
// Homogenous arrays: elements are all String, all List or all Map. 
// Map values must be String, List or Map and can be mixed with the same Map.
// Arbitrary nesting.
// Top level element is a Map.


function add_map_table_to_node(parent_node:TreeViewNode, jmap:DynamicAccess<Dynamic>) {
	var index:Int = 0;
	for (key in jmap.keys()) {
		var val:Dynamic = jmap.get(key);
		trace('add_map_table_to_node: key[${key}]');
		var row_node = parent_node.addNode({text:'Key[$key]:Value[$val]'});
		if (++index > 5) {
			break;
		}
	}
}

function add_list_table_to_node(parent_node:TreeViewNode, jmap:DynamicAccess<Dynamic>) {
	var index:Int = 0;
	for (row in jmap) {
		trace('add_list_table_to_node: row[${row}]');
		var row_node = parent_node.addNode({text:'$row'});
		if (++index > 5) {
			break;
		}
	}
}


// jmap will look like....
// {"quandl": {"yield_csv": {"Type": "PrimaryKeyCSV", "CacheValue": 
//   {"2023-05-25":{"10 YR":"3.83","2 YR":"4.5","5 YR":"3.9","1 MO":"5.95","3 MO":"5.38","20 YR":"4.16"...
//
function update_treeview(parent_node:TreeView, jmap:DynamicAccess<Dynamic>) {
	var recursions:Int = 0;
	for (group_name in jmap.keys()) {
		// group_name is "quandl" in example above
		trace('update_treeview: group_name[${group_name}]');
		var group:DynamicAccess<Dynamic> = jmap.get(group_name);
		var group_node:TreeViewNode = parent_node.addNode({text:group_name});
		group_node.expanded = true;
		for (cache_entry_name in group.keys()) {
			var cache_entry = group.get(cache_entry_name);
        	trace('update_treeview: group_name[${group_name}] cache_key[${cache_entry_name}] type[${cache_entry.Type}]');            
			var cache_entry_node:TreeViewNode = group_node.addNode({text:cache_entry_name});
			switch ( cache_entry.Type) {
				case "PrimaryKeyCSV":
					add_map_table_to_node(cache_entry_node, cache_entry.CacheValue);
				case "RegularCSV":
					add_list_table_to_node(cache_entry_node, cache_entry.CacheValue);
					continue;
			}
		}
	}
}
