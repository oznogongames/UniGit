﻿using System.IO;
using System.Linq;
using Assets.Plugins.UniGit.Editor.Hooks;
using LibGit2Sharp;
using UniGit.Adapters;
using UniGit.Settings;
using UniGit.Utils;
using UnityEditor;
using UnityEngine;
using UnityEngine.Profiling;

namespace UniGit
{
	[InitializeOnLoad]
	public static class UniGitLoader
	{
		public static GitLfsManager LfsManager;
		public static readonly GitManager GitManager;
		public static GitHookManager HookManager;
		public static GitCredentialsManager CredentialsManager;
		public static GitExternalManager ExternalManager;
		public static GitLfsHelper LfsHelper;
		private static readonly InjectionHelper injectionHelper;
		public static GitAsyncManager AsyncManager;

		static UniGitLoader()
		{
			Profiler.BeginSample("UniGit Initialization");
			try
			{
				injectionHelper = new InjectionHelper();
				GitWindows.Init();
				var recompileChecker = ScriptableObject.CreateInstance<AssemblyReloadScriptableChecker>();
				recompileChecker.OnBeforeReloadAction = OnBeforeAssemblyReload;

				string repoPath = Application.dataPath.Replace(UniGitPath.UnityDeirectorySeparatorChar + "Assets", "").Replace(UniGitPath.UnityDeirectorySeparatorChar, Path.DirectorySeparatorChar);
				string settingsPath = UniGitPath.Combine(repoPath, ".git", "UniGit", "Settings.json");

				injectionHelper.Bind<string>().FromInstance(repoPath).WithId("repoPath");
				injectionHelper.Bind<string>().FromInstance(settingsPath).WithId("settingsPath");

				injectionHelper.Bind<GitCallbacks>().FromMethod(() =>
				{
					var c = new GitCallbacks();
					EditorApplication.update += c.IssueEditorUpdate;
					c.RefreshAssetDatabase += AssetDatabase.Refresh;
					c.SaveAssetDatabase += AssetDatabase.SaveAssets;
					EditorApplication.projectWindowItemOnGUI += c.IssueProjectWindowItemOnGUI;
					//asset postprocessing
					GitAssetPostprocessors.OnWillSaveAssetsEvent += c.IssueOnWillSaveAssets;
					GitAssetPostprocessors.OnPostprocessImportedAssetsEvent += c.IssueOnPostprocessImportedAssets;
					GitAssetPostprocessors.OnPostprocessDeletedAssetsEvent += c.IssueOnPostprocessDeletedAssets;
					GitAssetPostprocessors.OnPostprocessMovedAssetsEvent += c.IssueOnPostprocessMovedAssets;
					return c;
				});
				injectionHelper.Bind<IGitPrefs>().To<UnityEditorGitPrefs>();
				injectionHelper.Bind<GitManager>();
				injectionHelper.Bind<GitSettingsJson>();
				injectionHelper.Bind<GitSettingsManager>();
				injectionHelper.Bind<GitAsyncManager>();

				GitManager = injectionHelper.GetInstance<GitManager>();
				GitManager.Callbacks.RepositoryCreate += OnRepositoryCreate;

				GitUnityMenu.Init(GitManager);
				GitResourceManager.Initilize();
				GitOverlay.Initlize();

				//credentials
				injectionHelper.Bind<ICredentialsAdapter>().To<WincredCredentialsAdapter>();
				injectionHelper.Bind<GitCredentialsManager>();
				//externals
				injectionHelper.Bind<IExternalAdapter>().To<GitExtensionsAdapter>();
				injectionHelper.Bind<IExternalAdapter>().To<TortoiseGitAdapter>();
				injectionHelper.Bind<GitExternalManager>();
				injectionHelper.Bind<GitLfsManager>();
				//hooks
				injectionHelper.Bind<GitPushHookBase>().To<GitLfsPrePushHook>();
				injectionHelper.Bind<GitHookManager>();
				//helpers
				injectionHelper.Bind<GitLfsHelper>();
				injectionHelper.Bind<FileLinesReader>();
				//project window overlays
				injectionHelper.Bind<GitProjectOverlay>();

				if (!Repository.IsValid(repoPath))
				{
					EditorApplication.delayCall += OnDelayedInit;
				}
				else
				{
					Rebuild(injectionHelper);
					EditorApplication.delayCall += OnDelayedInit;
				}
			}
			finally
			{
				Profiler.EndSample();
			}
		}

		private static void Rebuild(InjectionHelper injectionHelper)
		{
			var settingsManager = injectionHelper.GetInstance<GitSettingsManager>();
			settingsManager.LoadGitSettings();

			//delayed called must be used for serialized properties to be loaded
			EditorApplication.delayCall += () =>
			{
				settingsManager.LoadOldSettingsFile();
			};

			HookManager = injectionHelper.GetInstance<GitHookManager>();
			LfsManager = injectionHelper.GetInstance<GitLfsManager>();
			ExternalManager = injectionHelper.GetInstance<GitExternalManager>();
			CredentialsManager = injectionHelper.GetInstance<GitCredentialsManager>();
			LfsHelper = injectionHelper.GetInstance<GitLfsHelper>();
			AsyncManager = injectionHelper.GetInstance<GitAsyncManager>();

			injectionHelper.GetInstance<GitAutoFetcher>();
			injectionHelper.GetInstance<GitProjectOverlay>();

			GitProjectContextMenus.Init(GitManager, ExternalManager);
		}

		private static void OnDelayedInit()
		{
			Profiler.BeginSample("UniGit Delayed Window Injection");
			try
			{
				//inject all windows that are open
				//windows should add themselves on OnEnable
				foreach (var editorWindow in GitWindows.Windows)
				{
					injectionHelper.Inject(editorWindow);
				}
			}
			finally
			{
				Profiler.EndSample();
			}

			//call delayed call here after all loaded delayed calls have been made
			GitManager.Callbacks.IssueDelayCall();
		}

		private static void OnRepositoryCreate()
		{
			Rebuild(injectionHelper);
		}

		private static void OnBeforeAssemblyReload()
		{
			
		}

	    public static T FindWindow<T>() where T : EditorWindow
	    {
	        var editorWindow = Resources.FindObjectsOfTypeAll<T>().FirstOrDefault();
	        if (editorWindow != null)
	        {
	            return editorWindow;
	        }
	        return null;
	    }

	    public static T GetWindow<T>() where T : EditorWindow
	    {
	        return GetWindow<T>(false);
	    }

	    public static T GetWindow<T>(bool utility) where T : EditorWindow
	    {
	        var editorWindow = Resources.FindObjectsOfTypeAll<T>().FirstOrDefault();
	        if (editorWindow != null)
	        {
		        editorWindow.Show();
				return editorWindow;
	        }
	        var newWindow = ScriptableObject.CreateInstance<T>();
            injectionHelper.Inject(newWindow);
            if(utility)
                newWindow.ShowUtility();
            else
	            newWindow.Show();

            return newWindow;
	    }

	    public static T DisplayWizard<T>(string title, string createButtonName) where T : ScriptableWizard
	    {
	        return DisplayWizard<T>(title, createButtonName, "");
	    }

	    public static T DisplayWizard<T>(string title,string createButtonName,string otherButtonName) where T : ScriptableWizard
	    {
	        var instance = ScriptableWizard.DisplayWizard<T>(title, createButtonName, otherButtonName);
            injectionHelper.Inject(instance);
            return instance;
        }
	}
}