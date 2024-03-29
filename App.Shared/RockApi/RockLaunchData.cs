using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using System.IO;
using Rock.Mobile.Network;
using App.Shared.Notes.Model;
using RestSharp;
using App.Shared.Config;
using Rock.Mobile.IO;
using MobileApp;
using Rock.Mobile.Util.Strings;

namespace App
{
    namespace Shared
    {
        namespace Network
        {
            /// <summary>
            /// Stores data that can safely be used as placeholders for
            /// areas of the app that normally require a network connection.
            /// </summary>
            public sealed class RockLaunchData
            {
                private static RockLaunchData _Instance = new RockLaunchData( );
                public static RockLaunchData Instance { get { return _Instance; } }

                const string LAUNCH_DATA_FILENAME = "mobilelaunchdata.dat";

                // wrapper for managing the data obtained at launch
                public class LaunchData
                {
                    public LaunchData( )
                    {
                        //ALWAYS INCREMENT THIS IF UPDATING THE MODEL
                        ClientModelVersion = 1;
                        //

                        GeneralDataServerTime = DateTime.MinValue;

                        News = new List<RockNews>( );
                        NoteDB = new NoteDB( );

                        // for the hardcoded news, leave OFF the image extensions, so that we can add them with scaling for iOS.
                        DefaultNews = new List<RockNews>( );
                        DefaultNews.Add( new RockNews( 
                            NewsConfig.DefaultNews_A[ 0 ], 

                            NewsConfig.DefaultNews_A[ 1 ],

                            NewsConfig.DefaultNews_A[ 2 ],

                            false,
                            false,
                            false,

                            "",
                            NewsConfig.DefaultNews_A[ 3 ],

                            "",
                            NewsConfig.DefaultNews_A[ 4 ],

                            new List<System.Guid>( ) ) );

                        DefaultNews.Add( new RockNews( 
                            NewsConfig.DefaultNews_B[ 0 ], 

                            NewsConfig.DefaultNews_B[ 1 ],

                            NewsConfig.DefaultNews_B[ 2 ],

                            false,
                            false,
                            false,

                            "",
                            NewsConfig.DefaultNews_B[ 3 ],

                            "",
                            NewsConfig.DefaultNews_B[ 4 ],

                            new List<System.Guid>( ) ) );


                        DefaultNews.Add( new RockNews( 
                            NewsConfig.DefaultNews_C[ 0 ], 

                            NewsConfig.DefaultNews_C[ 1 ],

                            NewsConfig.DefaultNews_C[ 2 ],

                            false,
                            false,
                            false,

                            "",
                            NewsConfig.DefaultNews_C[ 3 ],

                            "",
                            NewsConfig.DefaultNews_C[ 4 ],

                            new List<System.Guid>( ) ) );
                    }

                    /// <summary>
                    /// Copies the hardcoded default news into the News list,
                    /// so that there is SOMETHING for the user to see. Should only be done
                    /// if there is no news available after getting launch data.
                    /// </summary>
                    public void CopyDefaultNews( )
                    {
                        // COPY the general items into our own new list.
                        foreach ( RockNews newsItem in DefaultNews )
                        {
                            RockNews copiedNews = new RockNews( newsItem );
                            News.Add( copiedNews );

                            // also cache the compiled in main and header images so the News system can get them transparently
                            #if __IOS__
                            string mainImageName;
                            string headerImageName;
                            if( UIKit.UIScreen.MainScreen.Scale > 1 )
                            {
                                mainImageName = string.Format( "{0}/{1}@{2}x.png", Foundation.NSBundle.MainBundle.BundlePath, copiedNews.ImageName, UIKit.UIScreen.MainScreen.Scale );
                                headerImageName = string.Format( "{0}/{1}@{2}x.png", Foundation.NSBundle.MainBundle.BundlePath, copiedNews.HeaderImageName, UIKit.UIScreen.MainScreen.Scale );
                            }
                            else
                            {
                                mainImageName = string.Format( "{0}/{1}.png", Foundation.NSBundle.MainBundle.BundlePath, copiedNews.ImageName, UIKit.UIScreen.MainScreen.Scale );
                                headerImageName = string.Format( "{0}/{1}.png", Foundation.NSBundle.MainBundle.BundlePath, copiedNews.HeaderImageName, UIKit.UIScreen.MainScreen.Scale );
                            }

                            #elif __ANDROID__
                            string mainImageName = copiedNews.ImageName + ".png";
                            string headerImageName = copiedNews.HeaderImageName + ".png";
                            #endif

                            // cache the main image
                            MemoryStream stream = Rock.Mobile.IO.AssetConvert.AssetToStream( mainImageName );
                            stream.Position = 0;
                            FileCache.Instance.SaveFile( stream, copiedNews.ImageName, FileCache.CacheFileNoExpiration );
                            stream.Dispose( );

                            // cache the header image
                            stream = Rock.Mobile.IO.AssetConvert.AssetToStream( headerImageName );
                            stream.Position = 0;
                            FileCache.Instance.SaveFile( stream, copiedNews.HeaderImageName, FileCache.CacheFileNoExpiration );
                            stream.Dispose( );
                        }
                    }

                    /// <summary>
                    /// The last time that GeneralData was updated by the server. Each time we run,
                    /// we'll check with the server to see if there's a newer server time. If there is,
                    /// we need to download GeneralData again.
                    /// </summary>
                    /// <value>The version.</value>
                    public DateTime GeneralDataServerTime { get; set; }

                    /// <summary>
                    /// Default news to display when there's no connection available
                    /// </summary>
                    /// <value>The news.</value>
                    public List<RockNews> News { get; set; }

                    /// <summary>
                    /// The core object that stores info about the sermon notes.
                    /// </summary>
                    public NoteDB NoteDB { get; set; }

                    /// <summary>
                    /// The last time the noteDB was downloaded. This helps us know whether to
                    /// update it or not, in case the user hasn't quit the app in days.
                    /// </summary>
                    public DateTime NoteDBTimeStamp { get; set; }

                    /// <summary>
                    /// Used on the app's first run, or there's no network connection
                    /// and no valid downloaded news to use.
                    /// </summary>
                    /// <value>The default news.</value>
                    List<RockNews> DefaultNews { get; set; }

                    /// <summary>
                    /// Private to the client, this should be updated if the model
                    /// changes at all, so that we don't attempt to load an older one when upgrading the app.
                    /// </summary>
                    [JsonProperty]
                    public int ClientModelVersion { get; protected set; }
                }
                public LaunchData Data { get; set; }

                /// <summary>
                /// True if the notedb.xml is in the process of being downloaded. This is so that
                /// if the user visits Messages WHILE we're downloading, we can wait instead of requesting it.
                /// </summary>
                public bool RequestingNoteDB { get; private set; }

                public RockLaunchData( )
                {
                    Data = new LaunchData( );
                }

                /// <summary>
                /// The news UI should immediatley hook into this on launch so we can notify when news is ready for display.
                /// NOT CURRENTLY USING IT. ONLY NEEDED IF WE WANT TO UPDATE THE NEWS _WHILE_ THE USER IS SITTING ON THE NEWS PAGE.
                /// </summary>
                public delegate void NewsItemsDownloaded( );
                public NewsItemsDownloaded NewsItemsDownloadedCallback { get; set; }

                /// <summary>
                /// Wrapper function for getting the basic things we need at launch (news, notes, etc.)
                /// If for some reason one of these fails, they will be called independantly by the appropriate systems
                /// (So if NoteDB fails, GetNoteDB will be called by Messages when the user taps on it)
                /// </summary>
                /// <param name="launchDataResult">Launch data result.</param>
                public void GetLaunchData( HttpRequest.RequestResult launchDataResult )
                {
                    Rock.Mobile.Util.Debug.WriteLine( "Get LaunchData" );

                    // first get the general data server time, so that we know whether we should update the
                    // general data or not.
                    MobileAppApi.GetGeneralDataTime( 
                        delegate(System.Net.HttpStatusCode statusCode, string statusDescription, DateTime generalDataTime )
                            {
                                if( Rock.Mobile.Network.Util.StatusInSuccessRange( statusCode ) )
                                {
                                    if( generalDataTime != DateTime.MinValue )
                                    {
                                        Data.GeneralDataServerTime = generalDataTime;
                                    }
                                }
                                else
                                {
                                    Rock.Mobile.Util.Debug.WriteLine( string.Format( "GeneralDateTime request failed with status {0}. Using existing.", statusDescription ) );
                                }

                                // now get the news.
                                GetNews( delegate 
                                    {
                                        // chain any other required launch data actions here.
                                        Rock.Mobile.Util.Debug.WriteLine( "Get LaunchData DONE" );

                                        // notify the caller now that we're done
                                        if( launchDataResult != null )
                                        {
                                            // send OK, because whether we failed or not, the caller doessn't need to care.
                                            launchDataResult( System.Net.HttpStatusCode.OK, "" );
                                        }
                                    });
                            } );
                }

                void GetNews( HttpRequest.RequestResult resultCallback )
                {
                    MobileAppApi.GetNews( 
                        delegate(System.Net.HttpStatusCode statusCode, string statusDescription, List<Rock.Client.ContentChannelItem> model )
                        {
                            if ( Rock.Mobile.Network.Util.StatusInSuccessRange( statusCode ) == true )
                            {
                                Rock.Mobile.Util.Debug.WriteLine( "Got news from Rock." );

                                // before comitting to this news, make sure there's at least one valid news item.
                                if( model.Count > 0 && model[ 0 ].AttributeValues != null )
                                {
                                    // sort it by priority
                                    model.Sort( delegate(Rock.Client.ContentChannelItem x, Rock.Client.ContentChannelItem y )
                                        {
                                            return x.Priority < y.Priority ? -1 : 1;
                                        } );
                                    
                                    // clear existing news
                                    Data.News.Clear( );

                                    // parse and take the new items
                                    foreach( Rock.Client.ContentChannelItem item in model )
                                    {
                                        // it's possible rock sent us bad data, so guard against any incomplete news items
                                        if( item.AttributeValues != null )
                                        {
                                            // we do this so we can store it on the stack and print it out if there's an exception.
                                            string currKey = "";

                                            try
                                            {
                                                currKey = "FeatureImage";
                                                string featuredGuid = item.AttributeValues[ currKey ].Value;
                                                string imageUrl = GeneralConfig.RockBaseUrl + "GetImage.ashx?Guid=" + featuredGuid;

                                                currKey = "PromotionImage";
                                                string bannerGuid = item.AttributeValues[ currKey ].Value;
                                                string bannerUrl = GeneralConfig.RockBaseUrl + "GetImage.ashx?Guid=" + bannerGuid;

                                                currKey = "DetailsURL";
                                                string detailUrl = item.AttributeValues[ currKey ].Value;

                                                currKey = "DetailsURLLaunchesBrowser";
                                                bool detailUrlLaunchesBrowser = bool.Parse( item.AttributeValues[ currKey ].Value );

                                                currKey = "IncludeImpersonationToken";
                                                bool includeImpersonationToken = bool.Parse( item.AttributeValues[ currKey ].Value );

                                                currKey = "MobileAppSkipDetailsPage";
                                                bool mobileAppSkipDetailsPage = bool.Parse( item.AttributeValues[ currKey ].Value );

                                                // take a list of the campuses that this news item should display for
                                                // (if the list is blank, we'll show it for all campuses)
                                                currKey = "Campuses";

                                                List<Guid> campusGuids = new List<Guid>( );
                                                if( item.AttributeValues[ currKey ] != null && string.IsNullOrEmpty( item.AttributeValues[ currKey ].Value ) == false )
                                                {
                                                    // this will be a comma-dilimited list of campuses to use for the news
                                                    string[] campusGuidList = item.AttributeValues[ currKey ].Value.Split( ',' );
                                                    foreach( string campusGuid in campusGuidList )
                                                    {
                                                        campusGuids.Add( Guid.Parse( campusGuid ) );
                                                    }
                                                }

                                                // jhm 11-30-15: Use the image guids, rather than news title, for the image.
                                                // This will ensure the image updates anytime it's changed in Rock!
                                                RockNews newsItem = new RockNews( item.Title, 
                                                                                  item.Content, 
                                                                                  detailUrl, 
                                                                                  mobileAppSkipDetailsPage,
                                                                                  detailUrlLaunchesBrowser,
                                                                                  includeImpersonationToken,
                                                                                  imageUrl, 
                                                                                  featuredGuid.AsLegalFilename( ) + ".png",//item.Title.AsLegalFilename( ) + "_main.png", 
                                                                                  bannerUrl, 
                                                                                  bannerGuid.AsLegalFilename( ) + ".png",//item.Title.AsLegalFilename( ) + "_banner.png", 
                                                                                  campusGuids );


                                                // handle developer fields

                                                // do a quick check and see if this should be flagged 'private'
                                                bool newsPublic = IsNewsPublic( item );
                                                newsItem.Developer_Private = !newsPublic;

                                                newsItem.Developer_StartTime = item.StartDateTime;
                                                newsItem.Developer_EndTime = item.ExpireDateTime;
                                                newsItem.Developer_ItemStatus = item.Status;

                                                Data.News.Add( newsItem );
                                            }
                                            catch( Exception e )
                                            {
                                                // one of the attribute values we wanted wasn't there. Package up what WAS there and report
                                                // the error. We can then use process of elimination to fix it.
                                                Rock.Mobile.Util.Debug.WriteLine( string.Format( "News Item Exception. Attribute Value not found is: {0}. Full Exception {1}", currKey, e ) );
#if !DEBUG
                                                string attribValues = JsonConvert.SerializeObject( item.AttributeValues );
                                                Exception reportException = new Exception( "News Item Exception. Attribute Value not found. Attribute Values found: " + attribValues, e );
                                                Xamarin.Insights.Report( reportException );
#endif
                                            }
                                        }
                                    }
                                }
                            }
                            else
                            {
                                Rock.Mobile.Util.Debug.WriteLine( "News request failed." );
                            }

                            if ( resultCallback != null )
                            {
                                resultCallback( statusCode, statusDescription );
                            }
                        } );
                }

                bool IsNewsPublic( Rock.Client.ContentChannelItem newsItem )
                {
                    // if the start time is valid
                    if( newsItem.StartDateTime <= DateTime.Now )
                    {
                        // and its approvated
                        if( newsItem.Status == Rock.Client.Enums.ContentChannelItemStatus.Approved )
                        {
                            return true;
                        }
                    }

                    return false;
                }

                // jhm hack: store the error so I can debug and figure this out.
                public static string HackNotesErrorCheck = "";

                public void GetNoteDB( HttpRequest.RequestResult resultCallback )
                {
                    RequestingNoteDB = true;

                    Rock.Mobile.Network.HttpRequest request = new HttpRequest();
                    RestRequest restRequest = new RestRequest( Method.GET );
                    restRequest.RequestFormat = DataFormat.Xml;

                    request.ExecuteAsync<NoteDB>( GeneralConfig.NoteBaseURL + "note_db.xml", restRequest, 
                        delegate(System.Net.HttpStatusCode statusCode, string statusDescription, NoteDB noteModel )
                        {
                            if ( Rock.Mobile.Network.Util.StatusInSuccessRange( statusCode ) == true && noteModel != null && noteModel.SeriesList.Count > 0 )
                            {
                                Rock.Mobile.Util.Debug.WriteLine( "Got NoteDB info." );
                                Data.NoteDB = noteModel;
                                Data.NoteDB.ProcessPrivateNotes( App.Shared.Network.RockGeneralData.Instance.Data.DeveloperModeEnabled );
                                Data.NoteDB.MakeURLsAbsolute( );
                                Data.NoteDBTimeStamp = DateTime.Now;

                                // download the first note so the user can immediately access it without having to wait
                                // for other crap.
                                if( Data.NoteDB.SeriesList[ 0 ].Messages.Count > 0 && 
                                    string.IsNullOrEmpty( Data.NoteDB.SeriesList[ 0 ].Messages[ 0 ].NoteUrl ) == false )
                                {
                                    App.Shared.Notes.Note.TryDownloadNote( Data.NoteDB.SeriesList[ 0 ].Messages[ 0 ].NoteUrl, Data.NoteDB.HostDomain, true, delegate
                                        {
                                            RequestingNoteDB = false;

                                            if ( resultCallback != null )
                                            {
                                                resultCallback( statusCode, statusDescription );
                                            }
                                        });
                                }
                                else
                                {
                                    Rock.Mobile.Util.Debug.WriteLine( "No note for latest message." );

                                    RequestingNoteDB = false;

                                    if ( resultCallback != null )
                                    {
                                        resultCallback( statusCode, statusDescription );
                                    }
                                }
                            }
                            else if ( noteModel == null || noteModel.SeriesList.Count == 0 )
                            {
                                statusDescription = "NoteDB downloaded but failed parsing.";
                                statusCode = System.Net.HttpStatusCode.BadRequest;
                                Rock.Mobile.Util.Debug.WriteLine( statusDescription );

                                RequestingNoteDB = false;

                                if ( resultCallback != null )
                                {
                                    resultCallback( statusCode, statusDescription );
                                }

                                // jhm hack: store the error so I can debug and figure this out.
                                if( noteModel == null )
                                {
                                    HackNotesErrorCheck = "Code 1";
                                }
                                else if ( noteModel.SeriesList.Count == 0 )
                                {
                                    HackNotesErrorCheck = "Code 2";
                                }
                            }
                            else
                            {
                                // jhm hack: store the error so I can debug and figure this out.
                                HackNotesErrorCheck = "Code 3: " + statusCode;
                                
                                Rock.Mobile.Util.Debug.WriteLine( "NoteDB request failed." );
                                RequestingNoteDB = false;

                                if ( resultCallback != null )
                                {
                                    resultCallback( statusCode, statusDescription );
                                }
                            }
                        } );
                }

                /// <summary>
                /// Returns true if there ARE no series in the note DB, or if the last time the noteDB
                /// was downloaded was too long ago.
                /// </summary>
                public bool NeedSeriesDownload( )
                {
                    // if the series hasn't been downloaded yet, or it's older than a day, redownload it.
                    TimeSpan seriesDelta = DateTime.Now - Data.NoteDBTimeStamp;
                    if ( Data.NoteDB.SeriesList.Count == 0 || seriesDelta.TotalDays >= 1 )
                    {
                        return true;
                    }

                    return false;
                }

                public void SaveToDevice( )
                {
                    string filePath = Path.Combine(System.Environment.GetFolderPath(System.Environment.SpecialFolder.Personal), LAUNCH_DATA_FILENAME);

                    // open a stream
                    using (StreamWriter writer = new StreamWriter(filePath, false))
                    {
                        string json = JsonConvert.SerializeObject( Data );
                        writer.WriteLine( json );
                    }
                }

                public void LoadFromDevice(  )
                {
                    // at startup, this should be called to allow current objects to be restored.
                    string filePath = Path.Combine(System.Environment.GetFolderPath(System.Environment.SpecialFolder.Personal), LAUNCH_DATA_FILENAME);

                    // if the file exists
                    if ( System.IO.File.Exists( filePath ) == true )
                    {
                        // read it
                        using ( StreamReader reader = new StreamReader( filePath ) )
                        {
                            string json = reader.ReadLine( );

                            try
                            {
                                // guard against the LaunchData changing and the user having old data.
                                LaunchData loadedData = JsonConvert.DeserializeObject<LaunchData>( json ) as LaunchData;
                                if( loadedData.ClientModelVersion == Data.ClientModelVersion )
                                {
                                    Data = loadedData;
                                }
                            }
                            catch( Exception )
                            {
                            }
                        }
                    }

                    // we HAVE to have news. So, if there isn't any after loading,
                    // take the general data's news.
                    if ( Data.News.Count == 0 )
                    {
                        Data.CopyDefaultNews( );
                    }
                }
            }
        }
    }
}
