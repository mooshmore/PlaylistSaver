﻿using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using PlaylistSaver.Windows.ViewModels;
using Google.Apis.YouTube.v3;
using Google;
using PlaylistSaver.PlaylistMethods;
using Google.Apis.YouTube.v3.Data;
using Helpers;
using System.Linq;

namespace PlaylistSaver.PlaylistMethods
{
    internal class PlaylistData
    {
        internal static async Task<List<Playlist>> RetrieveOwnedPlaylistsData()
        {
            PlaylistListResponse playlists = null;
            string nextPageToken = null;

            do
            {
                PlaylistsResource.ListRequest playlistListRequest = OAuthLogin.youtubeService.Playlists.List(part: "contentDetails,id,snippet,status");
                playlistListRequest.Mine = true;

                PlaylistListResponse currentPlaylistListResponse = await playlistListRequest.ExecuteAsync();
                nextPageToken = currentPlaylistListResponse.NextPageToken;

                // Assign the object on the first run
                if (playlists == null)
                    playlists = currentPlaylistListResponse;
                // Merge the object on the next runs
                else
                {
                    foreach (var playlist in currentPlaylistListResponse.Items)
                    {
                        playlists.Items.Add(playlist);
                    }
                }
            } while (nextPageToken != null);

            // Convert the default google plyalist class to a one used in the program
            return ParsePlaylists(playlists.Items).Result;
        }
        internal static async Task<List<Playlist>> RetrievePlaylistsData(List<string> playlistIds)
        {
            // Just in case make so that the playlist ids won't repeat
            playlistIds = playlistIds.Distinct().ToList();

            // Contains a total list of channels in a form of a request list that are currently being retrieved
            string currentPlaylistsList = "";
            PlaylistListResponse playlists = null;

            int playlistCount = 0;
            foreach (string playlistId in playlistIds)
            {
                playlistCount++;
                currentPlaylistsList += $"{playlistId},";

                // Data of max 50 channels can be retrieved at once
                // Retrieve the data if the count has reached 50 or the item is the last retrieved item
                if (playlistCount == 50 || playlistIds.IsLastItem(playlistId))
                {
                    playlistCount = 0;
                    GetPlaylistsData(currentPlaylistsList.TrimToLast(",")).Wait();
                    currentPlaylistsList = "";
                }
            }

            // Convert the default google plyalist class to a one used in the program
            return ParsePlaylists(playlists.Items).Result;

            // Retrieves data for the given playlists
            async Task GetPlaylistsData(string currentPlaylistsList)
            {
                PlaylistsResource.ListRequest playlistListRequest = OAuthLogin.youtubeService.Playlists.List(part: "contentDetails,id,snippet,status");
                playlistListRequest.Id = currentPlaylistsList;


                PlaylistListResponse currentPlaylistListResponse;
                try
                {
                    currentPlaylistListResponse = await playlistListRequest.ExecuteAsync();
                }
                catch (GoogleApiException requestError)
                {
                    switch (requestError.Error.Code)
                    {
                        case 404:
                            // playlist not found
                            // private playlist or incorrect link
                            //return (false, null);
                            break;
                    }
                    throw;
                }

                // Assign the object on the first run
                if (playlists == null)
                    playlists = currentPlaylistListResponse;
                // Merge the object on the next runs
                else
                {
                    foreach (var playlist in currentPlaylistListResponse.Items)
                    {
                        playlists.Items.Add(playlist);
                    }
                }
            }
        }

        /// <summary>
        /// Converts the default youtube api playlists object to the one used in the program.
        /// </summary>
        internal static async Task<List<Playlist>> ParsePlaylists(IList<Google.Apis.YouTube.v3.Data.Playlist> youtubePlaylists)
        {
            List<Playlist> playlistsList = new();
            foreach (var item in youtubePlaylists)
            {
                playlistsList.Add(new Playlist(item));
            }
            return playlistsList;
        }

        /// <summary>
        /// Saves the playlist data locally - that includes info about the playlist in a form of a .json file and 
        /// downloading and saving the playlist thumbnail.
        /// </summary>
        /// <param name="playlistsList">The list of playlists to save information about.</param>
        internal static async void SavePlaylistData(List<Playlist> playlistsList)
        {
            // Save data for every playlist
            foreach (Playlist playlist in playlistsList)
            {
                DirectoryInfo playlistDirectory = GlobalItems.playlistsDirectory.CreateSubdirectory(playlist.PlaylistInfo.Id);

                // Youtube playlist thumbnails have different url when they are changed, so only redownload
                // the thumbnail if the url(id to be precise) doesn't match or the thumbnail doesn't exist
                if (!playlistDirectory.SubfileExists_Prefix(playlist.ThumbnailInfo.Id))
                {
                    // Remove the thumbnail if the thumbnail Id doesn't match (playlistInfo is overwritten anyways)
                    playlistDirectory.DeleteAllSubfiles(".jpg");
                    // Save the thumbnail   
                    GlobalItems.WebClient.DownloadFile(playlist.ThumbnailInfo.URL, Path.Combine(playlistDirectory.FullName, playlist.ThumbnailInfo.FileName));
                }

                // Serialize the playlist data into a json
                string jsonString = JsonConvert.SerializeObject(playlist);
                // Create a new playlistInfo.json file and write the playlist data to it
                File.WriteAllText(playlistDirectory.CreateSubfile("plyalistInfo.json").FullName, jsonString);
            }
        }
    }
}