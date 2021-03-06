﻿//-------------------------------------------------------------------------------------------------
// <copyright file="ProgressViewModel.cs" company="Outercurve Foundation">
//   Copyright (c) 2004, Outercurve Foundation.
//   This software is released under Microsoft Reciprocal License (MS-RL).
//   The license and further copyright text can be found in the file
//   LICENSE.TXT at the root directory of the distribution.
// </copyright>
//-------------------------------------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Text.RegularExpressions;
using WixToolset.Bootstrapper;

namespace WixToolset.UX
{
    public class ProgressViewModel : PropertyNotifyBase
    {
        private static readonly Regex TrimActionTimeFromMessage = new Regex(@"^\w+\s+\d+:\d+:\d+:\s+", RegexOptions.CultureInvariant | RegexOptions.ExplicitCapture | RegexOptions.Singleline);

        private RootViewModel root;
        private Dictionary<string, int> executingPackageOrderIndex;

        private int progressPhases;
        private int progress;
        private int cacheProgress;
        private int executeProgress;
        private string package;
        private string message;

        public ProgressViewModel(RootViewModel root)
        {
            this.root = root;
            this.executingPackageOrderIndex = new Dictionary<string, int>();

            this.root.PropertyChanged += this.RootPropertyChanged;

            WixBA.Model.Bootstrapper.ExecutePackageBegin += this.ExecutePackageBegin;
            WixBA.Model.Bootstrapper.ExecuteMsiMessage += this.ExecuteMsiMessage;
            WixBA.Model.Bootstrapper.ExecuteProgress += this.ApplyExecuteProgress;
            WixBA.Model.Bootstrapper.PlanBegin += this.PlanBegin;
            WixBA.Model.Bootstrapper.PlanPackageComplete += this.PlanPackageComplete;
            WixBA.Model.Bootstrapper.ApplyBegin += this.ApplyBegin;
            WixBA.Model.Bootstrapper.Progress += this.ApplyProgress;
            WixBA.Model.Bootstrapper.CacheAcquireProgress += this.CacheAcquireProgress;
            WixBA.Model.Bootstrapper.CacheComplete += this.CacheComplete;
        }

        public bool ProgressEnabled
        {
            get { return this.root.InstallState == InstallationState.Applying; }
        }

        public int Progress
        {
            get
            {
                return this.progress;
            }

            set
            {
                if (this.progress != value)
                {
                    this.progress = value;
                    base.OnPropertyChanged("Progress");
                }
            }
        }

        public string Package
        {
            get
            {
                return this.package;
            }

            set
            {
                if (this.package != value)
                {
                    this.package = value;
                    base.OnPropertyChanged("Package");
                }
            }
        }

        public string Message
        {
            get
            {
                return this.message;
            }

            set
            {
                if (this.message != value)
                {
                    this.message = value;
                    base.OnPropertyChanged("Message");
                }
            }
        }

        void RootPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if ("InstallState" == e.PropertyName)
            {
                base.OnPropertyChanged("ProgressEnabled");
            }
        }

        private void PlanBegin(object sender, PlanBeginEventArgs e)
        {
            lock (this)
            {
                this.executingPackageOrderIndex.Clear();
            }
        }

        private void PlanPackageComplete(object sender, PlanPackageCompleteEventArgs e)
        {
            if (ActionState.None != e.Execute)
            {
                lock (this)
                {
                    Debug.Assert(!this.executingPackageOrderIndex.ContainsKey(e.PackageId));
                    this.executingPackageOrderIndex.Add(e.PackageId, this.executingPackageOrderIndex.Count);
                }
            }
        }

        private void ExecutePackageBegin(object sender, ExecutePackageBeginEventArgs e)
        {
            this.Package = WixBA.Model.GetPackageName(e.PackageId);

            e.Result = this.root.Canceled ? Result.Cancel : Result.Ok;
        }

        private void ExecuteMsiMessage(object sender, ExecuteMsiMessageEventArgs e)
        {
            lock (this)
            {
                if (e.MessageType == InstallMessage.ActionStart)
                {
                    string message = TrimActionTimeFromMessage.Replace(e.Message, String.Empty);
                    this.Message = message;
                }

                e.Result = this.root.Canceled ? Result.Cancel : Result.Ok;
            }
        }

        private void ApplyBegin(object sender, ApplyBeginEventArgs e)
        {
            this.progressPhases = e.PhaseCount;
        }

        private void ApplyProgress(object sender, ProgressEventArgs e)
        {
            lock (this)
            {
                e.Result = this.root.Canceled ? Result.Cancel : Result.Ok;
            }
        }

        private void CacheAcquireProgress(object sender, CacheAcquireProgressEventArgs e)
        {
            lock (this)
            {
                this.cacheProgress = e.OverallPercentage;
                this.Progress = (this.cacheProgress + this.executeProgress) / this.progressPhases;
                e.Result = this.root.Canceled ? Result.Cancel : Result.Ok;
            }
        }

        private void CacheComplete(object sender, CacheCompleteEventArgs e)
        {
            lock (this)
            {
                this.cacheProgress = 100;
                this.Progress = (this.cacheProgress + this.executeProgress) / this.progressPhases;
            }
        }

        private void ApplyExecuteProgress(object sender, ExecuteProgressEventArgs e)
        {
            lock (this)
            {
                this.executeProgress = e.OverallPercentage;
                this.Progress = (this.cacheProgress + this.executeProgress) / this.progressPhases;

                if (WixBA.Model.Command.Display == Display.Embedded)
                {
                    WixBA.Model.Engine.SendEmbeddedProgress(e.ProgressPercentage, this.Progress);
                }

                e.Result = this.root.Canceled ? Result.Cancel : Result.Ok;
            }
        }
    }
}
