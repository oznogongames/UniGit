﻿using System;
using LibGit2Sharp;
using UniGit.Utils;
using UnityEditor;
using UnityEngine;

namespace UniGit
{
	public class GitCheckoutWindowPopup : PopupWindowContent
	{
		[SerializeField] private bool force;
		private readonly Branch branch;
		private readonly GitManager gitManager;

		public GitCheckoutWindowPopup(GitManager gitManager,Branch branch)
		{
			this.gitManager = gitManager;
			this.branch = branch;
		}

		public override Vector2 GetWindowSize()
		{
			return new Vector2(256,74);
		}

		public override void OnGUI(Rect rect)
		{
			GUILayout.Label(GitGUI.GetTempContent("Switch to: " + branch.FriendlyName), GitGUI.Styles.BigTitle,GUILayout.ExpandWidth(true));
			force = EditorGUILayout.Toggle(GitGUI.GetTempContent("Force", "Override working tree changes"), force);
			if (GUILayout.Button(GitGUI.GetTempContent("Siwtch")))
			{
				CheckoutOptions checkoutOptions = new CheckoutOptions()
				{
					OnCheckoutNotify = OnCheckoutNotify,
					OnCheckoutProgress = OnCheckoutProgress
				};

				if (force)
					checkoutOptions.CheckoutModifiers = CheckoutModifiers.Force;

				if (branch != null)
				{
					try
					{
						gitManager.Repository.Checkout(branch, checkoutOptions);
						Debug.Log("Switched to branch: " + branch.FriendlyName);
					}
					catch (Exception e)
					{
						Debug.LogError("There was a problem while switching to branch: " + branch.CanonicalName);
						Debug.LogException(e);
					}
					finally
					{
						gitManager.Callbacks.IssueAssetDatabaseRefresh();
						gitManager.MarkDirty(true);
					}
				}
				else
				{
					Debug.LogError("Trying to switch to null branch");
				}
			}
		}

		protected bool OnCheckoutNotify(string path, CheckoutNotifyFlags notifyFlags)
		{
			Debug.Log(path + " (" + notifyFlags + ")");
			return true;
		}

		protected void OnCheckoutProgress(string path, int completedSteps, int totalSteps)
		{
			float percent = (float)completedSteps / totalSteps;
			EditorUtility.DisplayProgressBar("Checkout", string.Format("Checking {0} steps out of {1}.", completedSteps, totalSteps), percent);
		}
	}
}