var PluginTest = {
    $PluginsVal: {
    },

    Recording_Start: function () { StartMic(); },
	Recording_Stop: function() { StopMic();},

	Recording_UpdatePointer: function(idx)
	{
		floatPCMPointer = idx;
	}
}

autoAddDeps(PluginTest, '$PluginsVal');
mergeInto(LibraryManager.library, PluginTest);