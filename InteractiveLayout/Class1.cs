using System;
using System.Linq.Dynamic.Core;
using Xamarin.Forms;

namespace InteractiveLayout
{
    public class InteractiveLayout : ContentView
    {
        /// <summary>
        /// Value representing the current X translation of the content.
        /// </summary>
        private double _currentX;

        /// <summary>
        /// Value representing the current Y translation of the content.
        /// </summary>
        private double _currentY;

        /// <summary>
        /// Value used to help align the X translation of the content.
        /// </summary>
        private double _offsetX;

        /// <summary>
        /// Value used to help align the Y translation of the content.
        /// </summary>
        private double _offsetY;

        /// <summary>
        /// Maximum value the content is allowed to be translated on the X axis.
        /// </summary>
        private double _widthBound;

        /// <summary>
        /// Maximum value the content is allowed to be translated on the Y axis.
        /// </summary>
        private double _heightBound;

        /// <summary>
        /// Flag to determine if the content is currently being panned.
        /// </summary>
        private bool _isContentPanning;

        /// <summary>
        /// Factor used to adjust the zooming rate of the pinch gesture.
        /// </summary>
        private const double ScaleFactor = 1.5;

        /// <summary>
        /// Allowable margin for Left and Right translations before EventHandlers are invoked. 
        /// </summary>
        private const double XBoundLimit = 150;

        /// <summary>
        /// Allowable margin for Up and Down translations before EventHandlers are invoked. 
        /// </summary>
        private const double YBoundLimit = 100;

        /// <summary>
        /// Item Collection containing the InteractiveLayout.
        /// NOTE: This is used to allow the InteractiveLayout to complement the CarouselView, but isn't necessary for utilization. 
        /// </summary>
        public CarouselView ItemsCollection { get; set; }

        /// <summary>
        /// Event to be executed when the content is shifted to the right past the X Bound Limit.
        /// </summary>
        public event EventHandler<PanUpdatedEventArgs> RightShifted;

        /// <summary>
        /// Event to be executed when the content is shifted to the left past the X Bound Limit.
        /// </summary>
        public event EventHandler<PanUpdatedEventArgs> LeftShifted;

        /// <summary>
        /// Event to be executed when the content is shifted up past the Y Bound Limit.
        /// </summary>
        public event EventHandler<PanUpdatedEventArgs> UpShifted;

        /// <summary>
        /// Event to be executed when the content is shifted down past the Y Bound Limit.
        /// </summary>
        public event EventHandler<PanUpdatedEventArgs> DownShifted;

        /// <summary>
        /// Layout that allows the user to Pan and Zoom its content.
        /// </summary>
        public InteractiveLayout()
        {
            /* Pinch Gesture Implementation */
            var pinchGesture = new PinchGestureRecognizer();
            pinchGesture.PinchUpdated += PinchUpdated;
            GestureRecognizers.Add(pinchGesture);

            /* Pan Gesture Implementation */
            var panGesture = new PanGestureRecognizer();
            panGesture.PanUpdated += PanUpdated;
            GestureRecognizers.Add(panGesture);
        }

        /// <summary>
        /// Resets both the scale of the layout and sets its translation values back to the center.
        /// </summary>
        public void ResetLayout()
        {
            /* Translations and Offset Reset */
            ResetBounds();
            ResetTranslations();
            ResetOffsets();

            /* Scale Reset */
            Content.ScaleTo(1);
        }

        /// <summary>
        /// Resets the current translation values and sets the layout back to the center.
        /// </summary>
        private void ResetTranslations()
        {
            /* Translation Value Reset */
            _currentX = 0;
            _currentY = 0;

            /* Layout Positioning Reset */
            Content.TranslateTo(0, 0);
        }

        /// <summary>
        /// Changes the layout scale while updating the layout bounds.
        /// </summary>
        /// <param name="pinchEventArgs">Arguments containing the Pinch Scale and other attributes</param>
        private void UpdateScale(PinchGestureUpdatedEventArgs pinchEventArgs)
        {
            /* Scale Adjustment */
            var pinchScale = ScaleFactor * (pinchEventArgs.Scale - 1);
            Content.Scale = Math.Max(1, Content.Scale + pinchScale);

            /* Boundaries Update */
            if (!Content.Scale.Equals(1))
            {
                SetBounds();
            }
            else
            {
                ResetBounds();
            }

            /* Y Translation Lock */
            // NOTE: This line of code ensures that the layout will be kept within the height bounds.
            Content.TranslationY = Math.Min(Math.Max(_currentY, -_heightBound), _heightBound);
        }

        /// <summary>
        /// Updates the scale of the content during pinch status. Will also rebound the layout within its bounds after
        /// pinch. 
        /// </summary>
        /// <param name="sender">View responsible for triggering this event</param>
        /// <param name="pinchEventArgs">Arguments containing the Pinch Scale and other attributes</param>
        /// <exception cref="ArgumentOutOfRangeException">Error to display during an unexpected gesture status</exception>
        private void PinchUpdated(object sender, PinchGestureUpdatedEventArgs pinchEventArgs)
        {
            /* Gesture Status Switch Case */
            switch (pinchEventArgs.Status)
            {
                case GestureStatus.Started:
                    // NOTE: `isContentPanning` is set to `false` to prevent the Pinch and Pan gestures from overlapping.
                    _isContentPanning = false;
                    break;
                case GestureStatus.Running:
                    UpdateScale(pinchEventArgs);
                    break;
                case GestureStatus.Completed:
                    BounceLayout();
                    break;
                default:
                    const string errorMessage = "Unknown Error with InteractiveLayout.PinchUpdated.";
                    throw new ArgumentOutOfRangeException(nameof(sender), errorMessage);
            }
        }

        /// <summary>
        /// Drags the layout around the screen to allow the user to view different portions of the layout. Accomplished
        /// by dynamically adjusting the translation values of the layout. 
        /// </summary>
        /// <param name="sender">View responsible for triggering this event</param>
        /// <param name="panEventArgs">Arguments containing attributes pertaining to Swipes and Drags</param>
        /// <exception cref="ArgumentOutOfRangeException">Error to display during an unexpected gesture status</exception>
        private void PanUpdated(object sender, PanUpdatedEventArgs panEventArgs)
        {
            /* Gesture Status Switch Case */
            switch (panEventArgs.StatusType)
            {
                case GestureStatus.Started:
                    SetOffsets(panEventArgs);
                    break;
                case GestureStatus.Running:
                    PanContent(panEventArgs);
                    break;
                case GestureStatus.Completed:
                    CompletePanGesture(sender, panEventArgs);
                    break;
                case GestureStatus.Canceled:
                    break;
                default:
                    const string errorMessage = "Unknown Error with InteractiveLayout.PanUpdated.";
                    throw new ArgumentOutOfRangeException(nameof(sender), errorMessage);
            }
        }

        /// <summary>
        /// Changes the translation values of the content according to the Pan Event Arguments.
        /// </summary>
        /// <param name="panEventArgs">Arguments containing attributes pertaining to Swipes and Drags</param>
        private void PanContent(PanUpdatedEventArgs panEventArgs)
        {
            /* Pan Check */
            // NOTE: This is implemented to make sure that the layout isn't getting pinched.
            if (_isContentPanning)
            {
                /* Item Collection */
                var collectionItems = ItemsCollection?.ItemsSource;
                var itemCount = collectionItems?.ToDynamicList().Count;

                /* Current Translation Value Update */
                _currentX = (panEventArgs.TotalX * Content.Scale) + _offsetX;
                _currentY = (panEventArgs.TotalY * Content.Scale) + _offsetY;

                // NOTE: This code is executed so that the end pages (first and last page) don't allow for unnecessary
                //  panning.
                if (collectionItems != null && ItemsCollection.Position == 0)
                {
                    _currentX = Math.Min(_currentX, _widthBound);
                }
                else if (collectionItems != null && ItemsCollection.Position == itemCount - 1)
                {
                    _currentX = Math.Max(_currentX, -_widthBound);
                }

                /* Content Translation Update */
                Content.TranslationX = _currentX;
                Content.TranslationY = Math.Min(Math.Max(_currentY, -_heightBound), _heightBound);
            }
            else
            {
                /* Offset Update */
                SetOffsets(panEventArgs);
            }

            /* Pan Check Update */
            _isContentPanning = true;
        }

        /// <summary>
        /// Completes the Pan Gesture by executing the directional shift method (if applicable) as well as returning the
        /// layout back in bounds.
        /// </summary>
        /// <param name="sender">View responsible for triggering this event</param>
        /// <param name="panEventArgs">Arguments containing attributes pertaining to Swipes and Drags</param>
        private void CompletePanGesture(object sender, PanUpdatedEventArgs panEventArgs)
        {
            if (_isContentPanning)
            {
                ExecuteShiftMethod(sender, panEventArgs);
                BounceLayout();
            }
        }

        /// <summary>
        /// Changes the offset values according to the Pan Event Arguments.
        /// </summary>
        /// <param name="panEventArgs">Arguments containing attributes pertaining to Swipes and Drags</param>
        private void SetOffsets(PanUpdatedEventArgs panEventArgs)
        {
            _offsetX = -(panEventArgs.TotalX * Content.Scale) + Content.TranslationX;
            _offsetY = -(panEventArgs.TotalY * Content.Scale) + Content.TranslationY;
        }

        /// <summary>
        /// Adjusts the boundaries of the layout according to the Content.Scale.
        /// </summary>
        private void SetBounds()
        {
            _widthBound = Content.Width * (Content.Scale - 1) * 0.5;
            _heightBound = Content.Height * (Content.Scale - 1) * 0.5;
        }

        /// <summary>
        /// Resets the boundaries of the layout.
        /// </summary>
        private void ResetBounds()
        {
            _widthBound = 0;
            _heightBound = 0;
        }

        /// <summary>
        /// Resets the offset values of the layout.
        /// </summary>
        private void ResetOffsets()
        {
            _offsetX = 0;
            _offsetY = 0;
        }

        /// <summary>
        /// Invokes the shift method according to the shift direction of the layout.
        /// </summary>
        /// <param name="sender">View responsible for triggering this event</param>
        /// <param name="panEventArgs">Arguments containing attributes pertaining to Swipes and Drags</param>
        private void ExecuteShiftMethod(object sender, PanUpdatedEventArgs panEventArgs)
        {
            /* Direction Shift Variables */
            var isRightInvoked = _currentX < -(_widthBound + XBoundLimit);
            var isLeftInvoked = _currentX > (_widthBound + XBoundLimit);
            var isUpInvoked = _currentY < -(_heightBound + YBoundLimit);
            var isDownInvoked = _currentY > (_heightBound + YBoundLimit);

            /* Shift Event Invocations */
            if (isRightInvoked)
            {
                RightShifted?.Invoke(sender, panEventArgs);
            }
            else if (isLeftInvoked)
            {
                LeftShifted?.Invoke(sender, panEventArgs);
            }
            else if (isUpInvoked)
            {
                UpShifted?.Invoke(sender, panEventArgs);
            }
            else if (isDownInvoked)
            {
                DownShifted?.Invoke(sender, panEventArgs);
            }
        }

        /// <summary>
        /// Moves the layout back to the boundary points.
        /// </summary>
        private void BounceLayout()
        {
            /* Direction Shift Variables */
            var isShiftedLeft = _currentX > _widthBound;
            var isShiftedRight = _currentX < -_widthBound;
            var isShiftedUp = _currentY < -_heightBound;
            var isShiftedDown = _currentY > _heightBound;

            /* Layout Translation Rebounds */
            if (isShiftedRight)
            {
                if (isShiftedUp)
                {
                    Content.TranslateTo(-_widthBound, -_heightBound);
                }
                else if (isShiftedDown)
                {
                    Content.TranslateTo(-_widthBound, _heightBound);
                }
                else
                {
                    Content.TranslateTo(-_widthBound, Content.TranslationY);
                }
            }
            else if (isShiftedLeft)
            {
                if (isShiftedUp)
                {
                    Content.TranslateTo(_widthBound, -_heightBound);
                }
                else if (isShiftedDown)
                {
                    Content.TranslateTo(_widthBound, _heightBound);
                }
                else
                {
                    Content.TranslateTo(_widthBound, Content.TranslationY);
                }
            }
            else if (isShiftedUp)
            {
                Content.TranslateTo(Content.TranslationX, -_heightBound);
            }
            else if (isShiftedDown)
            {
                Content.TranslateTo(Content.TranslationX, _heightBound);
            }

            // NOTE: Resets the layout if the scale has been reset to 1.
            if (Content.Scale.Equals(1))
            {
                ResetLayout();
            }
        }
    }
}