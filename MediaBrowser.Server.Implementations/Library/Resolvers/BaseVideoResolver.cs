﻿using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Library;
using MediaBrowser.Model.Entities;
using MediaBrowser.Naming.Common;
using MediaBrowser.Naming.Video;
using System;
using System.IO;

namespace MediaBrowser.Server.Implementations.Library.Resolvers
{
    /// <summary>
    /// Resolves a Path into a Video or Video subclass
    /// </summary>
    /// <typeparam name="T"></typeparam>
    public abstract class BaseVideoResolver<T> : Controller.Resolvers.ItemResolver<T>
        where T : Video, new()
    {
        protected readonly ILibraryManager LibraryManager;

        protected BaseVideoResolver(ILibraryManager libraryManager)
        {
            LibraryManager = libraryManager;
        }

        /// <summary>
        /// Resolves the specified args.
        /// </summary>
        /// <param name="args">The args.</param>
        /// <returns>`0.</returns>
        protected override T Resolve(ItemResolveArgs args)
        {
            return ResolveVideo<T>(args, true);
        }

        /// <summary>
        /// Resolves the video.
        /// </summary>
        /// <typeparam name="TVideoType">The type of the T video type.</typeparam>
        /// <param name="args">The args.</param>
        /// <param name="parseName">if set to <c>true</c> [parse name].</param>
        /// <returns>``0.</returns>
        protected TVideoType ResolveVideo<TVideoType>(ItemResolveArgs args, bool parseName)
              where TVideoType : Video, new()
        {
            // If the path is a file check for a matching extensions
            var parser = new Naming.Video.VideoResolver(new ExtendedNamingOptions(), new Naming.Logging.NullLogger());

            if (args.IsDirectory)
            {
                TVideoType video = null;
                VideoFileInfo videoInfo = null;

                // Loop through each child file/folder and see if we find a video
                foreach (var child in args.FileSystemChildren)
                {
                    var filename = child.Name;

                    if ((child.Attributes & FileAttributes.Directory) == FileAttributes.Directory)
                    {
                        if (IsDvdDirectory(filename))
                        {
                            videoInfo = parser.ResolveDirectory(args.Path);

                            if (videoInfo == null)
                            {
                                return null;
                            }

                            video = new TVideoType
                            {
                                Path = args.Path,
                                VideoType = VideoType.Dvd,
                                ProductionYear = videoInfo.Year
                            };
                            break;
                        }
                        if (IsBluRayDirectory(filename))
                        {
                            videoInfo = parser.ResolveDirectory(args.Path);

                            if (videoInfo == null)
                            {
                                return null;
                            }

                            video = new TVideoType
                            {
                                Path = args.Path,
                                VideoType = VideoType.BluRay,
                                ProductionYear = videoInfo.Year
                            };
                            break;
                        }
                    }
                }

                if (video != null)
                {
                    if (parseName)
                    {
                        video.Name = videoInfo.Name;
                    }
                    else
                    {
                        video.Name = Path.GetFileName(args.Path);
                    }

                    Set3DFormat(video, videoInfo);
                }

                return video;
            }
            else
            {
                var videoInfo = parser.ResolveFile(args.Path);

                if (videoInfo == null)
                {
                    return null;
                }

                var isShortcut = string.Equals(videoInfo.Container, "strm", StringComparison.OrdinalIgnoreCase);

                if (LibraryManager.IsVideoFile(args.Path) || videoInfo.IsStub || isShortcut)
                {
                    var type = string.Equals(videoInfo.Container, "iso", StringComparison.OrdinalIgnoreCase) || string.Equals(videoInfo.Container, "img", StringComparison.OrdinalIgnoreCase) ?
                        VideoType.Iso :
                        VideoType.VideoFile;

                    var path = args.Path;

                    var video = new TVideoType
                    {
                        VideoType = type,
                        Path = path,
                        IsInMixedFolder = true,
                        IsPlaceHolder = videoInfo.IsStub,
                        IsShortcut = isShortcut,
                        ProductionYear = videoInfo.Year
                    };

                    if (parseName)
                    {
                        video.Name = videoInfo.Name;
                    }
                    else
                    {
                        video.Name = Path.GetFileNameWithoutExtension(path);
                    }

                    if (videoInfo.IsStub)
                    {
                        if (string.Equals(videoInfo.StubType, "dvd", StringComparison.OrdinalIgnoreCase))
                        {
                            video.VideoType = VideoType.Dvd;
                        }
                        else if (string.Equals(videoInfo.StubType, "hddvd", StringComparison.OrdinalIgnoreCase))
                        {
                            video.VideoType = VideoType.HdDvd;
                        }
                        else if (string.Equals(videoInfo.StubType, "bluray", StringComparison.OrdinalIgnoreCase))
                        {
                            video.VideoType = VideoType.BluRay;
                        }
                    }

                    Set3DFormat(video, videoInfo);

                    return video;
                }
            }

            return null;
        }

        private void Set3DFormat(Video video, VideoFileInfo videoInfo)
        {
            if (videoInfo.Is3D)
            {
                if (string.Equals(videoInfo.Format3D, "fsbs", StringComparison.OrdinalIgnoreCase))
                {
                    video.Video3DFormat = Video3DFormat.FullSideBySide;
                }
                else if (string.Equals(videoInfo.Format3D, "ftab", StringComparison.OrdinalIgnoreCase))
                {
                    video.Video3DFormat = Video3DFormat.FullTopAndBottom;
                }
                else if (string.Equals(videoInfo.Format3D, "hsbs", StringComparison.OrdinalIgnoreCase))
                {
                    video.Video3DFormat = Video3DFormat.HalfSideBySide;
                }
                else if (string.Equals(videoInfo.Format3D, "htab", StringComparison.OrdinalIgnoreCase))
                {
                    video.Video3DFormat = Video3DFormat.HalfTopAndBottom;
                }
                else if (string.Equals(videoInfo.Format3D, "sbs", StringComparison.OrdinalIgnoreCase))
                {
                    video.Video3DFormat = Video3DFormat.HalfSideBySide;
                }
                else if (string.Equals(videoInfo.Format3D, "sbs3d", StringComparison.OrdinalIgnoreCase))
                {
                    video.Video3DFormat = Video3DFormat.HalfSideBySide;
                }
                else if (string.Equals(videoInfo.Format3D, "tab", StringComparison.OrdinalIgnoreCase))
                {
                    video.Video3DFormat = Video3DFormat.HalfTopAndBottom;
                }
            }
        }

        /// <summary>
        /// Determines whether [is DVD directory] [the specified directory name].
        /// </summary>
        /// <param name="directoryName">Name of the directory.</param>
        /// <returns><c>true</c> if [is DVD directory] [the specified directory name]; otherwise, <c>false</c>.</returns>
        protected bool IsDvdDirectory(string directoryName)
        {
            return string.Equals(directoryName, "video_ts", StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Determines whether [is blu ray directory] [the specified directory name].
        /// </summary>
        /// <param name="directoryName">Name of the directory.</param>
        /// <returns><c>true</c> if [is blu ray directory] [the specified directory name]; otherwise, <c>false</c>.</returns>
        protected bool IsBluRayDirectory(string directoryName)
        {
            return string.Equals(directoryName, "bdmv", StringComparison.OrdinalIgnoreCase);
        }
    }
}