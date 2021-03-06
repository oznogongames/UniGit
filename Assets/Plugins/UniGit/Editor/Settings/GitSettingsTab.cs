﻿using System;
using UniGit.Status;
using UniGit.Utils;
using UnityEngine;

namespace UniGit.Settings
{
	public abstract class GitSettingsTab : IDisposable
	{
		protected GitSettingsWindow settingsWindow;
		private bool hasFocused;
		private bool initilized;
		protected readonly GitManager gitManager;
		protected readonly GUIContent name;

		[UniGitInject]
		internal GitSettingsTab(GUIContent name,GitManager gitManager, GitSettingsWindow settingsWindow)
		{
			this.name = name;
			this.gitManager = gitManager;
			this.settingsWindow = settingsWindow;
			var callbacks = gitManager.Callbacks;
			callbacks.EditorUpdate += OnEditorUpdateInternal;
			callbacks.UpdateRepository += OnGitManagerUpdateInternal;
		}

		internal abstract void OnGUI(Rect rect, Event current);

		protected virtual void OnInitialize()
		{
			
		}

		public void OnFocus()
		{
			hasFocused = true;
		}

		public void OnLostFocus()
		{
			hasFocused = false;
			initilized = false;
		}

		public virtual void OnGitUpdate(GitRepoStatus status, string[] paths)
		{
			
		}

		private void OnGitManagerUpdateInternal(GitRepoStatus status, string[] paths)
		{
			//only update the window if it is initialized. That means opened and visible.
			//the editor window will initialize itself once it's focused
			if (!initilized || !gitManager.IsValidRepo) return;
			OnGitUpdate(status, paths);
		}

		private void OnEditorUpdateInternal()
		{
			//Only initialize if the editor Window is focused
			if (hasFocused && !initilized && gitManager.Repository != null)
			{
				var cachedStatus = gitManager.GetCachedStatus();
				initilized = true;
				if (!gitManager.IsValidRepo) return;
				OnInitialize();
				OnGitManagerUpdateInternal(cachedStatus, null);
			}
		}

		public void Dispose()
		{
			if(gitManager == null || gitManager.Callbacks == null) return;
			var callbacks = gitManager.Callbacks;
			callbacks.EditorUpdate -= OnEditorUpdateInternal;
			callbacks.UpdateRepository -= OnGitManagerUpdateInternal;
		}

		public GUIContent Name
		{
			get { return name; }
		}
	}
}