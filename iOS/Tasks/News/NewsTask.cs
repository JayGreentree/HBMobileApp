using System;
using UIKit;
using CoreGraphics;
using Foundation;
using App.Shared.Network;
using System.Collections.Generic;
using App.Shared.Config;
using App.Shared.PrivateConfig;
using MobileApp;
using Rock.Mobile.PlatformSpecific.Util;

namespace iOS
{
    public class NewsTask : Task
    {
        NewsMainUIViewController MainPageVC { get; set; }

        List<RockNews> News { get; set; }

        public NewsTask( string storyboardName ) : base( storyboardName )
        {
            MainPageVC = Storyboard.InstantiateViewController( "MainPageViewController" ) as NewsMainUIViewController;
            MainPageVC.Task = this;

            ActiveViewController = MainPageVC;

            News = new List<RockNews>( );
        }

        public override void MakeActive( TaskUINavigationController parentViewController, NavToolbar navToolbar, CGRect containerBounds )
        {
            base.MakeActive( parentViewController, navToolbar, containerBounds );

            MainPageVC.View.Bounds = containerBounds;

            // refresh our news from GeneralData
            ReloadNews( );

            // and provide it to the main page
            MainPageVC.UpdateNews( News );

            // set our current page as root
            parentViewController.PushViewController(MainPageVC, false);
        }

        /// <summary>
        /// Takes the news from LaunchData and populates the NewsPrimaryFragment with it.
        /// </summary>
        void ReloadNews( )
        {
            Rock.Mobile.Threading.Util.PerformOnUIThread( delegate
                {
                    Rock.Client.Campus campus = RockGeneralData.Instance.Data.CampusFromId( RockMobileUser.Instance.ViewingCampus );
                    Guid viewingCampusGuid = campus != null ? campus.Guid : Guid.Empty;

                    // provide the news to the viewer by COPYING it.
                    News.Clear( );
                    foreach ( RockNews newsItem in RockLaunchData.Instance.Data.News )
                    {
                        // if the list of campus guids contains the viewing campus, OR there are no guids set, allow it.
                        if ( newsItem.CampusGuids.Contains( viewingCampusGuid ) || newsItem.CampusGuids.Count == 0 )
                        {
                            // Limit the amount of news to display to MaxNews so we don't show so many we
                            // run out of memory. If DEVELOPER MODE is on, show them all.
                            if( News.Count < PrivateNewsConfig.MaxNews || App.Shared.Network.RockGeneralData.Instance.Data.DeveloperModeEnabled == true )
                            {
                                News.Add( new RockNews( newsItem ) );
                            }
                        }
                    }
                } );
        }

        public override void WillShowViewController(TaskUIViewController viewController)
        {
            base.WillShowViewController( viewController );

            // turn off the share & create buttons
            NavToolbar.SetShareButtonEnabled( false, null );
            NavToolbar.SetCreateButtonEnabled( false, null );

            // if it's the main page, disable the back button on the toolbar
            if ( viewController == MainPageVC )
            {
                NavToolbar.Reveal( false );
            }
            else if ( viewController as TaskWebViewController == null )
            {
                //NavToolbar.RevealForTime( 3.0f );
                NavToolbar.Reveal( true );
            }
        }

        public override void PerformAction(string action)
        {
            base.PerformAction(action);

            switch ( action )
            {
                case PrivateGeneralConfig.TaskAction_NewsReload:
                {
                    ReloadNews( );
                    break;
                }

                case PrivateGeneralConfig.TaskAction_CampusChanged:
                {
                    // since we changed campuses, go ahead and update the displayed news.
                    ReloadNews( );


                    MainPageVC.UpdateNews( News );

                    MainPageVC.LoadAndDownloadImages( );
                    MainPageVC.LayoutChanged( );
                    break;
                }
            }
        }

        public override void TouchesEnded(TaskUIViewController taskUIViewController, NSSet touches, UIEvent evt)
        {
            base.TouchesEnded(taskUIViewController, touches, evt);

            // if they touched a dead area, reveal the nav toolbar again.
            //NavToolbar.RevealForTime( 3.0f );
        }

        public override void MakeInActive( )
        {
            base.MakeInActive( );
        }

        public override void OnActivated( )
        {
            base.OnActivated( );

            ActiveViewController.OnActivated( );
        }

        public override void WillEnterForeground()
        {
            base.WillEnterForeground();

            ActiveViewController.WillEnterForeground( );
        }

        public override void AppOnResignActive()
        {
            base.AppOnResignActive( );

            ActiveViewController.AppOnResignActive( );
        }

        public override void AppDidEnterBackground()
        {
            base.AppDidEnterBackground();

            ActiveViewController.AppDidEnterBackground( );
        }

        public override void AppWillTerminate()
        {
            base.AppWillTerminate( );

            ActiveViewController.AppWillTerminate( );
        }
    }
}
