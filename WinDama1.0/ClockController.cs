using System;
using System.Windows.Controls;
using System.Windows.Threading;

namespace WinDama
{
    /// <summary>
    /// Owns chess-clock timers and clock labels.
    /// </summary>
    public sealed class ClockController
    {
        private readonly TextBlock player1ClockLabel;
        private readonly TextBlock player2ClockLabel;
        private readonly DispatcherTimer player1Timer;
        private readonly DispatcherTimer player2Timer;

        public ClockController(TextBlock player1ClockLabel, TextBlock player2ClockLabel)
        {
            this.player1ClockLabel = player1ClockLabel;
            this.player2ClockLabel = player2ClockLabel;

            player1Timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
            player1Timer.Tick += (_, _) => TickPlayer(1);

            player2Timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
            player2Timer.Tick += (_, _) => TickPlayer(-1);
        }

        public event Action<int>? PlayerTimedOut;

        public TimeSpan Player1TimeRemaining { get; private set; }
        public TimeSpan Player2TimeRemaining { get; private set; }

        public void Reset(TimeSpan initialTime)
        {
            Player1TimeRemaining = initialTime;
            Player2TimeRemaining = initialTime;
            RefreshPlayer1ClockUI();
            RefreshPlayer2ClockUI();
        }

        public void SetTimes(TimeSpan player1TimeRemaining, TimeSpan player2TimeRemaining)
        {
            Player1TimeRemaining = player1TimeRemaining;
            Player2TimeRemaining = player2TimeRemaining;
            RefreshPlayer1ClockUI();
            RefreshPlayer2ClockUI();
        }

        public TimeSpan GetRemaining(int player)
        {
            return player == 1 ? Player1TimeRemaining : Player2TimeRemaining;
        }

        public void RefreshPlayer1ClockUI()
        {
            if (player1ClockLabel != null)
            {
                player1ClockLabel.Text = Player1TimeRemaining.ToString(@"mm\:ss");
            }
        }

        public void RefreshPlayer2ClockUI()
        {
            if (player2ClockLabel != null)
            {
                player2ClockLabel.Text = Player2TimeRemaining.ToString(@"mm\:ss");
            }
        }

        public void StartForPlayer(int player)
        {
            if (player == 1)
            {
                StartPlayer1Timer();
            }
            else
            {
                StartPlayer2Timer();
            }
        }

        public void StartPlayer1Timer()
        {
            player2Timer.Stop();
            player1Timer.Start();
        }

        public void StartPlayer2Timer()
        {
            player1Timer.Stop();
            player2Timer.Start();
        }

        public void StopAll()
        {
            player1Timer.Stop();
            player2Timer.Stop();
        }

        private void TickPlayer(int player)
        {
            if (player == 1)
            {
                Player1TimeRemaining = Player1TimeRemaining.Subtract(TimeSpan.FromSeconds(1));
                if (Player1TimeRemaining < TimeSpan.Zero)
                {
                    Player1TimeRemaining = TimeSpan.Zero;
                }
                RefreshPlayer1ClockUI();
                if (Player1TimeRemaining.TotalSeconds <= 0)
                {
                    StopAll();
                    PlayerTimedOut?.Invoke(1);
                }
            }
            else
            {
                Player2TimeRemaining = Player2TimeRemaining.Subtract(TimeSpan.FromSeconds(1));
                if (Player2TimeRemaining < TimeSpan.Zero)
                {
                    Player2TimeRemaining = TimeSpan.Zero;
                }
                RefreshPlayer2ClockUI();
                if (Player2TimeRemaining.TotalSeconds <= 0)
                {
                    StopAll();
                    PlayerTimedOut?.Invoke(-1);
                }
            }
        }
    }
}
