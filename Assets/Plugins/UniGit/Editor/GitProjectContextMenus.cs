﻿using System;
using System.IO;
using System.Linq;
using JetBrains.Annotations;
using LibGit2Sharp;
using UnityEditor;
using UnityEngine;

namespace UniGit
{
	public static class GitProjectContextMenus
	{
		private static GitManager gitManager;
		private static GitExternalManager externalManager;

		internal static void Init(GitManager gitManager,GitExternalManager externalManager)
		{
			GitProjectContextMenus.gitManager = gitManager;
			GitProjectContextMenus.externalManager = externalManager;
		}

		[MenuItem("Assets/Git/Add", priority = 50), UsedImplicitly]
		private static void AddSelected()
		{
			string[] paths = Selection.assetGUIDs.Select(AssetDatabase.GUIDToAssetPath).SelectMany(GitManager.GetPathWithMeta).ToArray();
			gitManager.AutoStage(paths);
		}

		[MenuItem("Assets/Git/Add", true, priority = 50), UsedImplicitly]
		private static bool AddSelectedValidate()
		{
			if (gitManager == null || !gitManager.IsValidRepo) return false;
			string[] paths = Selection.assetGUIDs.Select(g => string.IsNullOrEmpty(Path.GetExtension(AssetDatabase.GUIDToAssetPath(g))) ? AssetDatabase.GUIDToAssetPath(g) + ".meta" : AssetDatabase.GUIDToAssetPath(g)).SelectMany(GitManager.GetPathWithMeta).ToArray();
			return paths.Any(g => GitManager.CanStage(gitManager.Repository.RetrieveStatus(g)));
		}

		[MenuItem("Assets/Git/Remove", priority = 50), UsedImplicitly]
		private static void RemoveSelected()
		{
			string[] paths = Selection.assetGUIDs.Select(AssetDatabase.GUIDToAssetPath).SelectMany(GitManager.GetPathWithMeta).ToArray();
			gitManager.AutoUnstage(paths);
		}

		[MenuItem("Assets/Git/Remove", true, priority = 50), UsedImplicitly]
		private static bool RemoveSelectedValidate()
		{
			if (gitManager == null || !gitManager.IsValidRepo) return false;
			string[] paths = Selection.assetGUIDs.Select(g => string.IsNullOrEmpty(Path.GetExtension(AssetDatabase.GUIDToAssetPath(g))) ? AssetDatabase.GUIDToAssetPath(g) + ".meta" : AssetDatabase.GUIDToAssetPath(g)).SelectMany(GitManager.GetPathWithMeta).ToArray();
			return paths.Any(g => GitManager.CanUnstage(gitManager.Repository.RetrieveStatus(g)));
		}

		[MenuItem("Assets/Git/Difference", priority = 65), UsedImplicitly]
		private static void SeeDifference()
		{
			gitManager.ShowDiff(AssetDatabase.GUIDToAssetPath(Selection.assetGUIDs[0]), externalManager);
		}

		[MenuItem("Assets/Git/Difference", true, priority = 65)]
		private static bool SeeDifferenceValidate()
		{
			if (gitManager == null || !gitManager.IsValidRepo) return false;
			if (Selection.assetGUIDs.Length != 1) return false;
			var entry = gitManager.Repository.Index[AssetDatabase.GUIDToAssetPath(Selection.assetGUIDs[0])];
			if (entry != null)
			{
				Blob blob = gitManager.Repository.Lookup(entry.Id) as Blob;
				if (blob == null) return false;
				return !blob.IsBinary;
			}
			return false;
		}

		[MenuItem("Assets/Git/Difference with previous version", priority = 65), UsedImplicitly]
		private static void SeeDifferencePrev()
		{
			gitManager.ShowDiffPrev(AssetDatabase.GUIDToAssetPath(Selection.assetGUIDs[0]), externalManager);
		}

		[MenuItem("Assets/Git/Difference with previous version", true, priority = 65), UsedImplicitly]
		private static bool SeeDifferencePrevValidate()
		{
			return SeeDifferenceValidate();
		}

		[MenuItem("Assets/Git/Revert", priority = 80), UsedImplicitly]
		private static void Revet()
		{
			var paths = Selection.assetGUIDs.Select(AssetDatabase.GUIDToAssetPath).SelectMany(GitManager.GetPathWithMeta).ToArray();
			if (externalManager.TakeRevert(paths))
			{
				gitManager.Callbacks.IssueAssetDatabaseRefresh();
				gitManager.MarkDirty(paths);
				return;
			}

			try
			{
				gitManager.Repository.CheckoutPaths("HEAD", paths, new CheckoutOptions() { CheckoutModifiers = CheckoutModifiers.Force, OnCheckoutProgress = OnRevertProgress });
			}
			finally
			{
				EditorUtility.ClearProgressBar();
			}
			
			gitManager.Callbacks.IssueAssetDatabaseRefresh();
			gitManager.MarkDirty(paths);
		}

		[MenuItem("Assets/Git/Revert",true, priority = 80), UsedImplicitly]
		private static bool RevetValidate()
		{
			if (gitManager == null || !gitManager.IsValidRepo) return false;
			return Selection.assetGUIDs.Select(AssetDatabase.GUIDToAssetPath).SelectMany(GitManager.GetPathWithMeta).Where(File.Exists).Select(e => gitManager.Repository.RetrieveStatus(e)).Any(e => GitManager.CanStage(e) | GitManager.CanUnstage(e));
		}

		private static void OnRevertProgress(string path, int currentSteps, int totalSteps)
		{
			float percent = (float)currentSteps / totalSteps;
			EditorUtility.DisplayProgressBar("Reverting File", string.Format("Reverting file {0} {1}%", path, (percent * 100).ToString("####")), percent);
			if (currentSteps >= totalSteps)
			{
				gitManager.MarkDirty();
				Type type = typeof(EditorWindow).Assembly.GetType("UnityEditor.ProjectBrowser");
				EditorWindow.GetWindow(type).ShowNotification(new GUIContent("Revert Complete!"));
			}
		}

		[MenuItem("Assets/Git/Blame/Object", priority = 100), UsedImplicitly]
		private static void BlameObject()
		{
			var path = Selection.assetGUIDs.Select(AssetDatabase.GUIDToAssetPath).FirstOrDefault();
			gitManager.ShowBlameWizard(path, externalManager);
		}

		[MenuItem("Assets/Git/Blame/Object", priority = 100,validate = true), UsedImplicitly]
		private static bool BlameObjectValidate()
		{
			if (gitManager == null || !gitManager.IsValidRepo) return false;
			var path = Selection.assetGUIDs.Select(AssetDatabase.GUIDToAssetPath).FirstOrDefault();
			return gitManager.CanBlame(path);
		}

		[MenuItem("Assets/Git/Blame/Meta", priority = 100), UsedImplicitly]
		private static void BlameMeta()
		{
			var path = Selection.assetGUIDs.Select(AssetDatabase.GUIDToAssetPath).Select(AssetDatabase.GetTextMetaFilePathFromAssetPath).FirstOrDefault();
			gitManager.ShowBlameWizard(path, externalManager);
		}

		[MenuItem("Assets/Git/Blame/Meta", priority = 100,validate = true), UsedImplicitly]
		private static bool BlameMetaValidate()
		{
			if (gitManager == null || !gitManager.IsValidRepo) return false;
			var path = Selection.assetGUIDs.Select(AssetDatabase.GUIDToAssetPath).Select(AssetDatabase.GetTextMetaFilePathFromAssetPath).FirstOrDefault();
			return gitManager.CanBlame(path);
		}
	}
}