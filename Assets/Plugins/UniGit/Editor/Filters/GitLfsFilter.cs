﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading;
using LibGit2Sharp;
using Debug = UnityEngine.Debug;

namespace UniGit.Filters
{
	public class GitLfsFilter : Filter
	{
		private readonly Dictionary<string, Process> processes = new Dictionary<string, Process>();
		private readonly Dictionary<string, FilterMode> modes = new Dictionary<string, FilterMode>();
		private readonly GitManager gitManager;
		private readonly GitLfsManager lfsManager;

		public GitLfsFilter(string name, IEnumerable<FilterAttributeEntry> attributes, GitLfsManager lfsManager, GitManager gitManager) : base(name, attributes)
		{
			this.gitManager = gitManager;
			this.lfsManager = lfsManager;
		}

		protected override void Clean(string path, string root, Stream input, Stream output)
		{
			if(!lfsManager.IsEnabled) return;
			try
			{
				Process process;
				FilterMode mode;
				if (!processes.TryGetValue(path,out process))
				{
					Debug.Log("Could not find lfs process for path: " + path + " when cleaning");
					return;
				}

				if (!modes.TryGetValue(path, out mode))
				{
					Debug.Log("Could not find lfs filter mode for path: " + path + " when cleaning");
					return;
				}

				if (mode != FilterMode.Clean)
				{
					Debug.LogError("Filter mode mismatch when cleaning for path: " + path);
				}

				// write file data to stdin
				input.CopyTo(process.StandardInput.BaseStream);
				input.Flush();
			}
			catch (Exception e)
			{
				Debug.LogError("LFS Clean Error!");
				Debug.LogException(e);
			}
		}

		protected override void Complete(string path, string root, Stream output)
		{
			if (!lfsManager.IsEnabled) return;
			try
			{
				Process process;
				FilterMode mode;

				if (!processes.TryGetValue(path, out process))
				{
					throw new Exception("Could not find lfs process for path: " + path);
				}

				if (!modes.TryGetValue(path, out mode))
				{
					throw new Exception("Could not find lfs filter mode for path: " + path);
				}

				try
				{
					process.StandardInput.Flush();
					process.StandardInput.Close();

					// finalize stdin and wait for git-lfs to finish
					if (mode == FilterMode.Clean)
					{
						// write git-lfs pointer for 'clean' to git or file data for 'smudge' to working copy
						process.StandardOutput.BaseStream.CopyTo(output);
						process.StandardOutput.BaseStream.Flush();
						process.StandardOutput.Close();
						output.Flush();
						output.Close();

						process.WaitForExit();
					}
					else if (mode == FilterMode.Smudge)
					{
						// write git-lfs pointer for 'clean' to git or file data for 'smudge' to working copy
						process.StandardOutput.BaseStream.CopyTo(output);
						process.StandardOutput.BaseStream.Flush();
						process.StandardOutput.Close();
						output.Flush();
						output.Close();

						process.WaitForExit();
					}
				}
				finally
				{
					process.Dispose();
				}
			}
			catch (ThreadAbortException)
			{
				
			}
			catch (Exception e)
			{
				Debug.LogError("LFS Complete Error!");
				Debug.LogException(e);
			}
			finally
			{
				processes.Remove(path);
				modes.Remove(path);
			}
		}

		protected override void Create(string path, string root, FilterMode mode)
		{
			if (!lfsManager.IsEnabled) return;
			try
			{
				var process = new Process();
				var startInfo = new ProcessStartInfo
				{
					FileName = "git-lfs",
					WorkingDirectory = gitManager.RepoPath,
					RedirectStandardInput = true,
					RedirectStandardOutput = true,
					RedirectStandardError = true,
					CreateNoWindow = true,
					UseShellExecute = false
				};

				// launch git-lfs smudge or clean
				switch (mode)
				{
					case FilterMode.Smudge:
						startInfo.Arguments = "smudge";
						break;
					case FilterMode.Clean:
						startInfo.Arguments = "clean";
						break;
					default:
						throw new ArgumentOutOfRangeException("mode");
				}

				process.StartInfo = startInfo;
				if (!process.Start())
				{
					Debug.LogError("Cound not start lfs process of type: " + mode + " for path: " + path);
				}
				else
				{
					if (processes.ContainsKey(path))
					{
						Debug.LogError("There is already lfs process for path: " + path);
						return;
					}
					processes.Add(path,process);
					modes.Add(path,mode);
				}
			}
			catch (Exception e)
			{
				Debug.LogError("LFS Create Error!");
				Debug.LogException(e);
			}
		}

		protected override void Smudge(string path, string root, Stream input, Stream output)
		{
			if (!lfsManager.IsEnabled) return;
			try
			{
				Process process;
				FilterMode mode;
				if (!processes.TryGetValue(path, out process))
				{
					Debug.Log("Could not find lfs process for path: " + path + " when smudging");
					return;
				}

				if (!modes.TryGetValue(path, out mode))
				{
					Debug.Log("Could not find lfs filter mode for path: " + path + " when smudging");
					return;
				}

				if (mode != FilterMode.Smudge)
				{
					Debug.LogError("Filter mode mismatch when smudging for path: " + path);
				}

				// write git-lfs pointer to stdin
				input.CopyTo(process.StandardInput.BaseStream);
				input.Flush();
			}
			catch (Exception e)
			{
				Debug.LogError("LFS Smudge Error!");
				Debug.LogException(e);
			}
		}
	}
}