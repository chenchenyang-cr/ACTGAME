using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.Experimental.GraphView;
using UnityEngine;

 namespace CombatEditor
{	
	public class AnimEventSearchProvider: ScriptableObject, ISearchWindowProvider
	{
	    public Type[] types;
	    public List<SearchTreeEntry> CreateSearchTree(SearchWindowContext context)
	    {
	        List<SearchTreeEntry> searchList = new List<SearchTreeEntry>();
	        searchList.Add(new SearchTreeGroupEntry(new GUIContent("List"), 0));

        Type[] normalTypes = types
            .Where(type => !typeof(AbilityEventObj_GameplayWindow).IsAssignableFrom(type) &&
                           !typeof(AbilityEventObj_PostFxTrack).IsAssignableFrom(type))
	            .OrderBy(type => type.Name)
	            .ToArray();
        Type[] gameplayTypes = types
	            .Where(type => typeof(AbilityEventObj_GameplayWindow).IsAssignableFrom(type) && !type.IsAbstract)
	            .OrderBy(type => type.Name)
            .ToArray();
        Type[] postFxTypes = types
            .Where(type => typeof(AbilityEventObj_PostFxTrack).IsAssignableFrom(type) && !type.IsAbstract)
            .OrderBy(type => type.Name)
            .ToArray();

	        for (int i = 0; i < normalTypes.Length; i++)
	        {
	            SearchTreeEntry entry = new SearchTreeEntry(
	                new GUIContent(ObjectNames.NicifyVariableName(normalTypes[i].Name.Replace("AbilityEventObj_", ""))));
	            entry.level = 1;
	            entry.userData = normalTypes[i];
	            searchList.Add(entry);
	        }

        if (gameplayTypes.Length > 0)
	        {
	            searchList.Add(new SearchTreeGroupEntry(new GUIContent("Gameplay"), 1));
            for (int i = 0; i < gameplayTypes.Length; i++)
	            {
	                SearchTreeEntry entry = new SearchTreeEntry(
	                    new GUIContent(ObjectNames.NicifyVariableName(
	                        gameplayTypes[i].Name.Replace("AbilityEventObj_", ""))));
	                entry.level = 2;
	                entry.userData = gameplayTypes[i];
                searchList.Add(entry);
            }

        }

        if (postFxTypes.Length > 0)
        {
            searchList.Add(new SearchTreeGroupEntry(new GUIContent("Post FX"), 1));
            for (int i = 0; i < postFxTypes.Length; i++)
            {
                string label = postFxTypes[i].Name.Replace("AbilityEventObj_PostFx", string.Empty);
                SearchTreeEntry entry = new SearchTreeEntry(
                    new GUIContent(ObjectNames.NicifyVariableName(label)));
                entry.level = 2;
                entry.userData = postFxTypes[i];
                searchList.Add(entry);
            }
        }
	        return searchList;
	    }
	
	    public Action<Type> OnSetIndexCallBack;
	    public bool OnSelectEntry(SearchTreeEntry SearchTreeEntry, SearchWindowContext context)
	    {
	        OnSetIndexCallBack?.Invoke(SearchTreeEntry.userData as Type);
	        return true;
	    }
	}
}
