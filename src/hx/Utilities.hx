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


function add_map_to_node(parent_node:TreeViewNode, jmap:DynamicAccess<Dynamic>, max_recursions:Int, recursions:Int) {
	if (recursions > max_recursions) {
		return;
	}
	var index:Int = 0;
	for (key in jmap.keys()) {
		var val:Dynamic = jmap.get(key);
		trace('add_map_to_node: key[${key}]');
		if (val is String) {
			var new_child_node = parent_node.addNode({text:'$key:$val'});
			new_child_node.expanded = true;
		}
		else if (val is Array) {
			var list_node:TreeViewNode = parent_node.addNode({text:key});
			add_list_to_node(list_node, val, max_recursions, ++recursions);
		}
		else {
			var map_node:TreeViewNode = parent_node.addNode({text:key});
			add_map_to_node(map_node, val, max_recursions, ++recursions);
		}
		if (++index > 3) {
			break;
		}
	}
}

function add_map_to_treeview(parent_node:TreeView, jmap:DynamicAccess<Dynamic>,  max_recursions:Int) {
	var recursions:Int = 0;
	for (key in jmap.keys()) {
		trace('add_map_to_treeview: key[${key}]');
		var val:Dynamic = jmap.get(key);
		if (val is String) {
			var child = parent_node.addNode({text:'$key:$val'});
			child.expanded = true;
		}
		else if (val is Array) {
			var list_node:TreeViewNode = parent_node.addNode({text:key});
			list_node.expanded = true;
			add_list_to_node(list_node, val, max_recursions, ++recursions);
		}
		else {
			var map_node:TreeViewNode = parent_node.addNode({text:key});
			map_node.expanded = true;
			add_map_to_node(map_node, val, max_recursions, ++recursions);
		}
	}
}

function add_list_to_node(parent_node:TreeViewNode, jlist:DynamicAccess<Dynamic>, max_recursions:Int, recursions:Int) {
	if (recursions > max_recursions) {
		return;
	}
	var index:Int = 0;
	for (item in jlist) {
		trace('add_list_to_node: index[${index}]');
		var val:Dynamic = item;
		if (val is String) {
			parent_node.addNode({text:'$index:$val'});
		}
		else if (val is Array) {
			var list_node:TreeViewNode = parent_node.addNode({text:'$index'});
			add_list_to_node(list_node, val, max_recursions, ++recursions);
		}
		else {
			var map_node:TreeViewNode = parent_node.addNode({text:'$index'});
			add_map_to_node(map_node, val, max_recursions, ++recursions);
		}
		index++;
	}
}
