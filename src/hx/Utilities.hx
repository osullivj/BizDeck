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


function add_object_to_node(parent_node:TreeViewNode, jmap:DynamicAccess<Dynamic>) {
	var index:Int = 0;
	for (key in jmap.keys()) {
		var val:Dynamic = jmap.get(key);
		trace('add_object_to_node: key[${key}], val[${val}]');
		var row_node = parent_node.addNode({text:'$key:$val'});
	}
}


// jmap will look like....
// { "type": "cache", 
//   "data": {
//     "quandl": {
//       "yield_csv": {
//         "type": "PrimaryKeyCSV", 
//         "count": 8386, 
//         "row_key": "Date", 
//         "headers": ["Date", "1MO", "2MO", "3MO", "6MO", "1YR", "2YR", "3YR", "5YR", "7YR", "10YR", "20YR", "30YR"]
//       }
//     }
//   }
// }
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
        	trace('update_treeview: group_name[${group_name}] cache_key[${cache_entry_name}] type[${cache_entry.type}]');            
			var cache_entry_node:TreeViewNode = group_node.addNode({text:cache_entry_name});
            add_object_to_node(cache_entry_node, cache_entry);
		}
	}
}
