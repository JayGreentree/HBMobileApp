using System;
using System.Collections.Generic;
using System.Xml;
using App.Shared;
using App.Shared.Notes.Styles;
using Rock.Mobile.UI;
using App.Shared.Config;
using System.Drawing;
using App.Shared.PrivateConfig;

namespace App
{
    namespace Shared
    {
        namespace Notes
        {
            /// <summary>
            /// UI Control that allows absolute layout of children relative to the Canvas' X/Y position.
            /// </summary>
            public class Canvas : BaseControl
            {
                /// <summary>
                /// List of owned and managed child controls.
                /// </summary>
                /// <value>The child controls.</value>
                protected List<IUIControl> ChildControls { get; set; }

                /// <summary>
                /// The view representing any surrounding border for the canvas.
                /// </summary>
                /// <value>The border view.</value>
                protected PlatformView BorderView { get; set; }

                /// <summary>
                /// The bounds (including position) of this control.
                /// </summary>
                /// <value>The bounds.</value>
                protected RectangleF Frame { get; set; }

                /// <summary>
                /// The alignment that child controls should take within this control.
                /// </summary>
                /// <value>The child horz alignment.</value>
                protected Alignment ChildHorzAlignment { get; set; }

                protected override void Initialize( )
                {
                    base.Initialize( );

                    ChildControls = new List<IUIControl>( );

                    ChildHorzAlignment = Alignment.Inherit;

                    BorderView = PlatformView.Create( );
                }

                public Canvas( CreateParams parentParams, XmlReader reader )
                {
                    Initialize( );

                    // Always get our style first
                    mStyle = parentParams.Style;
                    Styles.Style.ParseStyleAttributesWithDefaults( reader, ref mStyle, ref ControlStyles.mCanvas );

                    // check for attributes we support
                    RectangleF bounds = new RectangleF( );
                    SizeF parentSize = new SizeF( parentParams.Width, parentParams.Height );
                    ParseCommonAttribs( reader, ref parentSize, ref bounds );

                    // Get margins and padding
                    RectangleF padding;
                    RectangleF margin;
                    GetMarginsAndPadding( ref mStyle, ref parentSize, ref bounds, out margin, out padding );

                    // apply margins to as much of the bounds as we can (bottom must be done by our parent container)
                    ApplyImmediateMargins( ref bounds, ref margin, ref parentSize );
                    Margin = margin;

                    // check for border styling
                    int borderPaddingPx = 0;
                    if ( mStyle.mBorderColor.HasValue )
                    {
                        BorderView.BorderColor = mStyle.mBorderColor.Value;
                    }

                    if( mStyle.mBorderRadius.HasValue )
                    {
                        BorderView.CornerRadius = mStyle.mBorderRadius.Value;
                    }

                    if( mStyle.mBorderWidth.HasValue )
                    {
                        BorderView.BorderWidth = mStyle.mBorderWidth.Value;
                        borderPaddingPx = (int)Rock.Mobile.Graphics.Util.UnitToPx( mStyle.mBorderWidth.Value + PrivateNoteConfig.BorderPadding );
                    }

                    if( mStyle.mBackgroundColor.HasValue )
                    {
                        BorderView.BackgroundColor = mStyle.mBackgroundColor.Value;
                    }
                    //

                    // now calculate the available width based on padding. (Don't actually change our width)
                    float availableWidth = bounds.Width - padding.Left - padding.Width - (borderPaddingPx * 2);

                    // now read what our children's alignment should be
                    // check for alignment
                    string result = reader.GetAttribute( "ChildAlignment" );
                    if( string.IsNullOrEmpty( result ) == false )
                    {
                        switch( result )
                        {
                            case "Left":
                                ChildHorzAlignment = Alignment.Left;
                                break;
                            case "Right":
                                ChildHorzAlignment = Alignment.Right;
                                break;
                            case "Center":
                                ChildHorzAlignment = Alignment.Center;
                                break;
                            default:
                                ChildHorzAlignment = mStyle.mAlignment.Value;
                                break;
                        }
                    }
                    else
                    {
                        // if it wasn't specified, use OUR alignment.
                        ChildHorzAlignment = mStyle.mAlignment.Value;
                    }

                    // Parse Child Controls
                    bool finishedParsing = false;
                    while( finishedParsing == false && reader.Read( ) )
                    {
                        switch( reader.NodeType )
                        {
                            case XmlNodeType.Element:
                                {
                                    // let each child have our available width.
                                    Style style = new Style( );
                                    style = mStyle;
                                    style.mAlignment = ChildHorzAlignment;
                                    IUIControl control = Parser.TryParseControl( new CreateParams( this, availableWidth, parentParams.Height, ref style ), reader );
                                    if( control != null )
                                    {
                                        ChildControls.Add( control );
                                    }
                                    break;
                                }

                            case XmlNodeType.EndElement:
                                {
                                    // if we hit the end of our label, we're done.
                                    //if( reader.Name == "Canvas" || reader.Name == "C" )
                                    if( ElementTagMatches( reader.Name ) )
                                    {
                                        finishedParsing = true;
                                    }

                                    break;
                                }
                        }
                    }


                    // layout all controls
                    float yOffset = bounds.Y + padding.Top + borderPaddingPx; //vertically they should just stack
                    float height = 0;

                    // now we must center each control within the stack.
                    foreach( IUIControl control in ChildControls )
                    {
                        RectangleF controlFrame = control.GetFrame( );
                        RectangleF controlMargin = control.GetMargin( );

                        // horizontally position the controls according to their 
                        // requested alignment
                        Alignment controlAlignment = control.GetHorzAlignment( );

                        // adjust by our position
                        float xAdjust = 0;
                        switch( controlAlignment )
                        {
                            case Alignment.Center:
                                xAdjust = bounds.X + ( ( availableWidth / 2 ) - ( controlFrame.Width / 2 ) );
                                break;
                            case Alignment.Right:
                                xAdjust = bounds.X + ( availableWidth - (controlFrame.Width + controlMargin.Width) );
                                break;
                            case Alignment.Left:
                                xAdjust = bounds.X;
                                break;
                        }

                        // adjust the next sibling by yOffset
                        control.AddOffset( xAdjust + padding.Left + borderPaddingPx, yOffset );

                        // track the height of the grid by the control lowest control 
                        height = (control.GetFrame( ).Bottom +  + controlMargin.Height) > height ? (control.GetFrame( ).Bottom +  + controlMargin.Height) : height;
                    }

                    // we need to store our bounds. We cannot
                    // calculate them on the fly because we
                    // would lose any control defined offsets, which would throw everything off.
                    bounds.Height = height + padding.Height + borderPaddingPx;

                    // setup our bounding rect for the border
                    bounds = new RectangleF( bounds.X, 
                                             bounds.Y,
                                             bounds.Width, 
                                             bounds.Height);

                    // and store that as our bounds
                    BorderView.Frame = bounds;

                    Frame = bounds;

                    // store our debug frame
                    SetDebugFrame( Frame );

                    // sort everything
                    ChildControls.Sort( BaseControl.Sort );
                }

                public override IUIControl TouchesEnded( PointF touch )
                {
                    // let each child handle it
                    foreach( IUIControl control in ChildControls )
                    {
                        // if a child consumes it, stop and report it was consumed.
                        IUIControl consumingControl = control.TouchesEnded( touch );
                        if( consumingControl != null)
                        {
                            return consumingControl;
                        }
                    }

                    return null;
                }

                public override void AddOffset( float xOffset, float yOffset )
                {
                    base.AddOffset( xOffset, yOffset );

                    // position each interactive label relative to ourselves
                    foreach( IUIControl control in ChildControls )
                    {
                        control.AddOffset( xOffset, yOffset );
                    }

                    BorderView.Position = new PointF( BorderView.Position.X + xOffset,
                                                      BorderView.Position.Y + yOffset );

                    // update our bounds by the new offsets.
                    Frame = new RectangleF( Frame.X + xOffset, Frame.Y + yOffset, Frame.Width, Frame.Height );
                }

                public override void AddToView( object obj )
                {
                    BorderView.AddAsSubview( obj );

                    // let each child do the same thing
                    foreach( IUIControl control in ChildControls )
                    {
                        control.AddToView( obj );
                    }

                    TryAddDebugLayer( obj );
                }

                public override void RemoveFromView( object obj )
                {
                    BorderView.RemoveAsSubview( obj );

                    // let each child do the same thing
                    foreach( IUIControl control in ChildControls )
                    {
                        control.RemoveFromView( obj );
                    }

                    TryRemoveDebugLayer( obj );
                }

                public override RectangleF GetFrame( )
                {
                    return Frame;
                }

                public override bool ShouldShowBulletPoint( )
                {
                    // as a container, it wouldn't really make sense to show a bullet point.
                    return false;
                }

                public override void BuildHTMLContent( ref string htmlStream, ref string textStream, List<IUIControl> userNotes )
                {
                    // handle child controls
                    foreach( IUIControl control in ChildControls )
                    {
                        control.BuildHTMLContent( ref htmlStream, ref textStream, userNotes );

                        htmlStream += "<br><br>";
                        textStream += "\n\n";
                    }

                    // handle user notes
                    EmbedIntersectingUserNotes( ref htmlStream, ref textStream, userNotes );
                }

                public static bool ElementTagMatches(string elementTag)
                {
                    if ( elementTag == "C" || elementTag == "Canvas" )
                    {
                        return true;
                    }
                    return false;
                }

                protected override List<IUIControl> GetChildControls( )
                {
                    return ChildControls;
                }
            }
        }
    }
}
