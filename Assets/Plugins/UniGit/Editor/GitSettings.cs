﻿using System;
using System.Collections.Generic;
using UniGit.Settings;
using UnityEngine;
using UnityEngine.Serialization;

#pragma warning disable 618

namespace UniGit
{
	[Obsolete("Use 'GitSettingsJson' instead.")]
	public class GitSettings : ScriptableObject
	{
		[Tooltip("Auto stage changes for committing when an asset is modified")]
		public bool AutoStage = true;
		[Tooltip("Auto fetch repository changes when possible. This will tell you about changes to the remote repository without having to pull. This only works with the Credentials Manager.")]
		public bool AutoFetch = true;
		[Tooltip("The Maximum amount of commits show in the Git History Window. Use -1 for infinite commits.")]
		[Delayed]
		public int MaxCommits = 32;
		public ExternalsTypeEnum ExternalsType;
		public string ExternalProgram;
		public string CredentialsManager;
		[Tooltip("The maximum depth at which overlays will be shown in the Project Window. This means that folders at levels higher than this will not be marked as changed. -1 indicates no limit")]
		[Delayed]
		public int ProjectStatusOverlayDepth = 2;
		[Tooltip("Show status for empty folder meta files and auto stage them, if 'Auto stage' option is enabled.")]
		public bool ShowEmptyFolders;
		[Tooltip("Should Git status retrieval be multithreaded.")]
		public bool GitStatusMultithreaded = true;
		[Tooltip("Load Gavatars based on the committer's email address.")]
		public bool UseGavatar = true;
		[Tooltip("The maximum height the commit text area can expand to.")]
		public float MaxCommitTextAreaSize = 120;
		[Tooltip("Detect Renames. This will make UniGit detect rename changes of files. Note that this feature is not always working as expected do the the modular updating and how Git itself works.")]
		public bool DetectRenames = true;
		

		[Flags]
		[SerializeField]
		public enum ExternalsTypeEnum
		{
			Pull = 1 << 0,
			Push = 1 << 1,
			Fetch = 1 << 2,
			Merge = 1 << 3,
			Commit = 1 << 4,
			Switch = 1 << 5,
			Reset = 1 << 6,
			Revert = 1 << 7,
			Blame = 1 << 8,
			Diff = 1 << 9
		}
	}

	[Serializable]
	public class GitSettingsJson
	{
		public const ThreadingType DefalutThreadingType = ThreadingType.StatusList | ThreadingType.StatusListGui | ThreadingType.CommitListGui;
		public bool AutoStage;
		public bool AutoFetch;
		public ExternalsTypeEnum ExternalsType;
		public string ExternalProgram;
		public string CredentialsManager;
		public int ProjectStatusOverlayDepth = 2;
		public bool ShowEmptyFolders;
		public ThreadingType Threading = DefalutThreadingType;
		public bool UseGavatar = true;
		public float MaxCommitTextAreaSize = 120;
		public bool DetectRenames = true;
		public bool UseSimpleContextMenus;
		public bool ReadFromFile;
		public bool DisableGitLFS;
		public bool LazyMode;
		private bool isDirty;

		[Flags]
		[Serializable]
		public enum ExternalsTypeEnum
		{
			Pull = 1 << 0,
			Push = 1 << 1,
			Fetch = 1 << 2,
			Merge = 1 << 3,
			Commit = 1 << 4,
			Switch = 1 << 5,
			Reset = 1 << 6,
			Revert = 1 << 7,
			Blame = 1 << 8,
			Diff = 1 << 9
		}

		[Flags]
		[Serializable]
		public enum ThreadingType
		{
			Stage = 1,
			Unstage = 2,
			StatusList = 4,
			StatusListGui = 8,
			CommitListGui = 16
		}

		public void Copy(GitSettings settings)
		{
			AutoStage = settings.AutoStage;
			AutoFetch = settings.AutoFetch;
			ExternalsType = (ExternalsTypeEnum)settings.ExternalsType;
			ExternalProgram = settings.ExternalProgram;
			CredentialsManager = settings.CredentialsManager;
			ProjectStatusOverlayDepth = settings.ProjectStatusOverlayDepth;
			ShowEmptyFolders = settings.ShowEmptyFolders;
			Threading = (settings.GitStatusMultithreaded ? DefalutThreadingType : 0);
			UseGavatar = settings.UseGavatar;
			MaxCommitTextAreaSize = settings.MaxCommitTextAreaSize;
			DetectRenames = settings.DetectRenames;
		}

		internal void MarkDirty()
		{
			isDirty = true;
		}

		internal void ResetDirty()
		{
			isDirty = false;
		}

		internal bool IsDirty
		{
			get { return isDirty; }
		}
	}
}