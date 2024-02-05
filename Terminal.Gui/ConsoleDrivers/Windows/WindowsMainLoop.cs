namespace Terminal.Gui.ConsoleDrivers.Windows;

/// <summary>
/// MainLoop intended to be used with the <see cref="WindowsDriver"/>, and can
/// only be used on Windows.
/// </summary>
/// <remarks>
/// This implementation is used for WindowsDriver.
/// </remarks>
internal class WindowsMainLoop : IMainLoopDriver {
        readonly ManualResetEventSlim _eventReady = new ManualResetEventSlim (false);
        readonly ManualResetEventSlim _waitForProbe = new ManualResetEventSlim (false);
        MainLoop _mainLoop;
        readonly ConsoleDriver _consoleDriver;
        readonly WindowsConsole _winConsole;
        CancellationTokenSource _eventReadyTokenSource = new CancellationTokenSource ();
        CancellationTokenSource _inputHandlerTokenSource = new CancellationTokenSource ();

        // The records that we keep fetching
        readonly Queue<InputRecord []> _resultQueue = new ();

        /// <summary>
        /// Invoked when the window is changed.
        /// </summary>
        public EventHandler<SizeChangedEventArgs> WinChanged;

        public WindowsMainLoop (ConsoleDriver consoleDriver = null)
        {
                _consoleDriver = consoleDriver ?? throw new ArgumentNullException (nameof (consoleDriver));
                _winConsole = ((WindowsDriver)consoleDriver).WinConsole;
        }

        void IMainLoopDriver.Setup (MainLoop mainLoop)
        {
                _mainLoop = mainLoop;
                Task.Run (WindowsInputHandler, _inputHandlerTokenSource.Token);
#if HACK_CHECK_WINCHANGED
                Task.Run (CheckWinChange);
#endif
        }

        void WindowsInputHandler ()
        {
                while (_mainLoop != null) {
                        try {
                                if (!_inputHandlerTokenSource.IsCancellationRequested) {
                                        _waitForProbe.Wait (_inputHandlerTokenSource.Token);
                                }

                        } catch (OperationCanceledException) {
                                return;
                        } finally {
                                _waitForProbe.Reset ();
                        }

                        if (_resultQueue?.Count == 0) {
                                _resultQueue.Enqueue (_winConsole.ReadConsoleInput ());
                        }

                        _eventReady.Set ();
                }
        }

#if HACK_CHECK_WINCHANGED
        readonly ManualResetEventSlim _winChange = new ManualResetEventSlim (false);
        bool _winChanged;
        Size _windowSize;
        void CheckWinChange ()
        {
                while (_mainLoop != null) {
                        _winChange.Wait ();
                        _winChange.Reset ();

                        // Check if the window size changed every half second. 
                        // We do this to minimize the weird tearing seen on Windows when resizing the console
                        while (_mainLoop != null) {
                                // ReSharper disable once AsyncApostle.AsyncWait
                                Task.Delay (500).Wait ();
                                _windowSize = _winConsole.GetConsoleBufferWindow (out _);
                                if (_windowSize != Size.Empty && (_windowSize.Width != _consoleDriver.Cols
                                                                  || _windowSize.Height != _consoleDriver.Rows)) {
                                        break;
                                }
                        }

                        _winChanged = true;
                        _eventReady.Set ();
                }
        }
#endif

        void IMainLoopDriver.Wakeup ()
        {
                _eventReady.Set ();
        }

        bool IMainLoopDriver.EventsPending ()
        {
                _waitForProbe.Set ();
#if HACK_CHECK_WINCHANGED
                _winChange.Set ();
#endif
                if (_mainLoop.CheckTimersAndIdleHandlers (out var waitTimeout)) {
                        return true;
                }

                try {
                        if (!_eventReadyTokenSource.IsCancellationRequested) {
                                // Note: ManualResetEventSlim.Wait will wait indefinitely if the timeout is -1. The timeout is -1 when there
                                // are no timers, but there IS an idle handler waiting.
                                _eventReady.Wait (waitTimeout, _eventReadyTokenSource.Token);
                        }
                } catch (OperationCanceledException) {
                        return true;
                } finally {
                        _eventReady.Reset ();
                }

                if (!_eventReadyTokenSource.IsCancellationRequested) {
#if HACK_CHECK_WINCHANGED
                        return _resultQueue.Count > 0 || _mainLoop.CheckTimersAndIdleHandlers (out _) || _winChanged;
#else
                        return _resultQueue.Count > 0 || _mainLoop.CheckTimersAndIdleHandlers (out _);
#endif
                }

                _eventReadyTokenSource.Dispose ();
                _eventReadyTokenSource = new CancellationTokenSource ();
                return true;
        }

        void IMainLoopDriver.Iteration ()
        {
                while (_resultQueue.Count > 0) {
                        var inputRecords = _resultQueue.Dequeue ();
                        if (inputRecords is { Length: > 0 }) {
                                ((WindowsDriver)_consoleDriver).ProcessInput (inputRecords [0]);
                        }
                }
#if HACK_CHECK_WINCHANGED
                if (_winChanged) {
                        _winChanged = false;
                        WinChanged?.Invoke (this, new SizeChangedEventArgs (_windowSize));
                }
#endif
        }

        void IMainLoopDriver.TearDown ()
        {
                _inputHandlerTokenSource?.Cancel ();
                _inputHandlerTokenSource?.Dispose ();

                _eventReadyTokenSource?.Cancel ();
                _eventReadyTokenSource?.Dispose ();
                _eventReady?.Dispose ();

                _resultQueue?.Clear ();

#if HACK_CHECK_WINCHANGED
                _winChange?.Dispose ();
#endif
                //_waitForProbe?.Dispose ();

                _mainLoop = null;
        }
}
