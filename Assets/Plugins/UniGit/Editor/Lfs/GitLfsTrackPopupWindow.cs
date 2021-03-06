﻿using UniGit.Utils;
using UnityEditor;
using UnityEngine;

namespace UniGit
{
	public class GitLfsTrackPopupWindow : PopupWindowContent
	{
		private readonly EditorWindow focusWindow;
		private readonly GitLfsManager lfsManager;
		private string extension;

		public GitLfsTrackPopupWindow(EditorWindow focusWindow,GitLfsManager lfsManager)
		{
			this.focusWindow = focusWindow;
			this.lfsManager = lfsManager;
		}

		public override void OnGUI(Rect rect)
		{
			extension = EditorGUILayout.TextField(GitGUI.GetTempContent("Extension"), extension);
			GitGUI.StartEnable(!string.IsNullOrEmpty(extension));
			if (GUILayout.Button(GitGUI.GetTempContent("Track")))
			{
				lfsManager.Track(extension);
				focusWindow.Focus();
				focusWindow.ShowNotification(new GUIContent(extension + " now tracked by LFS."));
				lfsManager.Update();
			}
			GitGUI.EndEnable();
		}
	}
}