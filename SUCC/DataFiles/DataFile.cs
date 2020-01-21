﻿using SUCC.Abstractions;
using System;
using System.IO;

namespace SUCC
{
    /// <summary>
    /// Represents a SUCC file in system storage.
    /// </summary>
    public class DataFile : ReadableWritableDataFile, IDataFileOnDisk
    {
        /// <summary>
        /// Creates a new DataFile object corresponding to a SUCC file in system storage.
        /// </summary>
        /// <param name="path"> The path of the file. Can be either absolute or relative to the default path. </param>
        /// <param name="defaultFileText"> If there isn't already a file at the path, one can be created from the text supplied here. </param>
        public DataFile(string path, string defaultFileText = null)
        {
            path = Utilities.AbsolutePath(path);
            path = Path.ChangeExtension(path, Utilities.FileExtension);
            this.FilePath = path;

            if (!Utilities.SuccFileExists(path))
            {
                if (defaultFileText == null)
                {
                    Directory.CreateDirectory(new FileInfo(path).Directory.FullName);
                    File.Create(path).Close(); // create empty file on disk
                }
                else
                {
                    File.WriteAllText(path, defaultFileText);
                }
            }

            this.ReloadAllData();

            SetupWatcher(); // setup watcher AFTER file has been created
        }

        /// <summary>
        /// Creates a new DataFile object corresponding to a SUCC file in system storage, with the option to assign a default file.
        /// </summary>
        /// <param name="path"> The path of the file. Can be either absolute or relative to the default path. </param>
        /// <param name="defaultFile"> If there isn't already a file at the path, one can be created from the file in the Resources folder by this name. </param>
        /// <param name="autoSave"> If true, the DataFile will automatically save changes to disk with each Get or Set. Otherwise, you must call SaveAllData() manually. </param>
        /// <param name="autoReload"> If true, the DataFile will automatically reload when the file changes on disk. </param>
        public DataFile(string path, string defaultFile, bool autoSave = true, bool autoReload = false) : this(path, defaultFile, FileStyle.Default, autoSave, autoReload)
        {
        }

        /// <summary>
        /// Creates a new DataFile object corresponding to a SUCC file in system storage, with the options to have a custom FileStyle and to assign a default file.
        /// </summary>
        /// <param name="path"> The path of the file. Can be either absolute or relative to the default path. </param>
        /// <param name="defaultFile"> If there isn't already a file at the path, one can be created from the file in the Resources folder by this name. </param>
        /// <param name="style"> The rules for how this file styles newly saved data </param>
        /// <param name="autoSave"> If true, the DataFile will automatically save changes to disk with each Get or Set. Otherwise, you must call SaveAllData() manually. </param>
        /// <param name="autoReload"> If true, the DataFile will automatically reload when the file changes on disk. </param>
        public DataFile(string path, string defaultFile, FileStyle style, bool autoSave = true, bool autoReload = false) :
            this(
                path, style, autoSave, autoReload,
                defaultFileText: ResourcesUtilities.ReadTextFromFile(defaultFile))
        {
        }

        /// <inheritdoc/>
        protected override string GetSavedText()
        {
            if (File.Exists(FilePath))
                return File.ReadAllText(FilePath);

            return String.Empty;
        }

        /// <inheritdoc/>
        protected override void SetSavedText(string text)
        {
            File.WriteAllText(FilePath, text);

            // FileSystemWatcher.Changed takes several seconds to fire, so we use this.
            IgnoreNextFileReload = true;
        }





        #region IDataFileOnDisk implementation
        // this code is copied between DataFile and ReadOnlyDataFile.
        // todo: once we upgrade to c# 8, this can probably be abstracted to a default interface implementation.

        /// <inheritdoc/>
        public string FilePath { get; protected set; }
        /// <inheritdoc/>
        public string FileName => Path.GetFileNameWithoutExtension(FilePath);
        /// <inheritdoc/>
        public long SizeOnDisk => new FileInfo(FilePath).Length;
        /// <inheritdoc/>
        public event Action OnAutoReload;

        /// <inheritdoc/>
        public bool AutoReload
        {
            get => _AutoReload;
            set
            {
                _AutoReload = value;
                Watcher.EnableRaisingEvents = value;

                if (value == true)
                    IgnoreNextFileReload = false; // in case this was set to true while AutoReload was false
            }
        }
        bool _AutoReload = false;

        private FileSystemWatcher Watcher;
        private void SetupWatcher()
        {
            var info = new FileInfo(FilePath);
            Watcher = new FileSystemWatcher(path: info.DirectoryName, filter: info.Name);

            Watcher.NotifyFilter = NotifyFilters.LastWrite;
            Watcher.Changed += this.OnWatcherChanged;
            Watcher.EnableRaisingEvents = this.AutoReload;
        }

        // Watcher.Changed takes several seconds to fire, so we use this.
        private bool IgnoreNextFileReload;

        private void OnWatcherChanged(object idontcare, FileSystemEventArgs goaway)
        {
            if (!_AutoReload)
                return;

            if (IgnoreNextFileReload)
            {
                IgnoreNextFileReload = false;
                return;
            }

            ReloadAllData();
            OnAutoReload?.Invoke();
        }

        #endregion
    }
}