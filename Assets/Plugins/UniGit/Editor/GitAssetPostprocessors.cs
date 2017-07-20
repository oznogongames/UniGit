﻿using System.Linq;
using JetBrains.Annotations;
using LibGit2Sharp;
using UnityEditor;

namespace UniGit
{
	public static class GitAssetPostprocessors
	{
		public class GitAssetModificationPostprocessor : UnityEditor.AssetModificationProcessor
		{
			[UsedImplicitly]
			private static string[] OnWillSaveAssets(string[] paths)
			{
				var gitManager = GitManager.Instance;

				if (gitManager.Prefs.GetBool("UniGit_DisablePostprocess")) return paths;
				if (gitManager.Repository != null && paths != null && paths.Length > 0)
				{
					bool autoStage = gitManager.Settings != null && gitManager.Settings.AutoStage;
					string[] pathsFinal = paths.SelectMany(g => GitManager.GetPathWithMeta(g)).ToArray();
					if (pathsFinal.Length > 0)
					{
						if(autoStage) gitManager.Repository.Stage(pathsFinal);
						gitManager.MarkDirty(pathsFinal);
					}
				}
				return paths;
			}
		}

		public class GitBrowserAssetPostprocessor : AssetPostprocessor
		{
			[UsedImplicitly]
			static void OnPostprocessAllAssets(string[] importedAssets, string[] deletedAssets, string[] movedAssets, string[] movedFromAssetPaths)
			{
				var gitManager = GitManager.Instance;

				if (gitManager.Prefs.GetBool("UniGit_DisablePostprocess")) return;
				if (gitManager.Repository != null)
				{
					bool autoStage = gitManager.Settings != null && gitManager.Settings.AutoStage;

					if (gitManager.Settings != null)
					{
						if (importedAssets != null && importedAssets.Length > 0)
						{
							string[] importedAssetsToStage = importedAssets.Where(a => !GitManager.IsEmptyFolder(a)).SelectMany(g => GitManager.GetPathWithMeta(g)).ToArray();
							if (importedAssetsToStage.Length > 0)
							{
								//todo add multi threaded staging 
								if(autoStage) gitManager.Repository.Stage(importedAssetsToStage);
								gitManager.MarkDirty(importedAssetsToStage);
							}
						}

						if (movedAssets != null && movedAssets.Length > 0)
						{
							string[] movedAssetsFinal = movedAssets.Where(a => !GitManager.IsEmptyFolder(a)).SelectMany(g => GitManager.GetPathWithMeta(g)).ToArray();
							if (movedAssetsFinal.Length > 0)
							{
								if(autoStage) gitManager.Repository.Stage(movedAssetsFinal);
								gitManager.MarkDirty(movedAssetsFinal);
							}
						}
					}

					//automatic deletion of previously moved asset is necessary even if AutoStage is off
					if (movedFromAssetPaths != null && movedFromAssetPaths.Length > 0)
					{
						string[] movedFromAssetPathsFinal = movedFromAssetPaths.SelectMany(g => GitManager.GetPathWithMeta(g)).ToArray();
						if (movedFromAssetPathsFinal.Length > 0)
						{
							gitManager.Repository.Unstage(movedFromAssetPathsFinal);
							gitManager.MarkDirty(movedFromAssetPathsFinal);
						}
					}

					//automatic deletion is necessary even if AutoStage is off
					if (deletedAssets != null && deletedAssets.Length > 0)
					{
						string[] deletedAssetsFinal = deletedAssets.SelectMany(g => GitManager.GetPathWithMeta(g)).ToArray();
						if (deletedAssetsFinal.Length > 0)
						{
							gitManager.Repository.Unstage(deletedAssetsFinal);
							gitManager.MarkDirty(deletedAssetsFinal);
						}
					}
				}
			}
		}
	}
}