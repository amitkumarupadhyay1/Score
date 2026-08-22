using System;
using System.Diagnostics;

namespace Score
{
    public enum QuizTimerState
    {
        Stopped,
        Running,
        Paused,
        Overtime,
        Completed
    }

    public sealed class QuizTimerService
    {
        private readonly Stopwatch stopwatch = new Stopwatch();
        private TimeSpan accumulated;
        private TimeSpan duration;

        public QuizTimerState State { get; private set; } = QuizTimerState.Stopped;

        public TimeSpan Duration
        {
            get { return duration; }
            set { duration = value < TimeSpan.Zero ? TimeSpan.Zero : value; }
        }

        public TimeSpan Elapsed
        {
            get { return accumulated + (stopwatch.IsRunning ? stopwatch.Elapsed : TimeSpan.Zero); }
        }

        public bool IsOvertime
        {
            get { return Duration > TimeSpan.Zero && Elapsed >= Duration; }
        }

        public TimeSpan Remaining
        {
            get
            {
                var remaining = Duration - Elapsed;
                return remaining > TimeSpan.Zero ? remaining : TimeSpan.Zero;
            }
        }

        public void Start(TimeSpan timerDuration)
        {
            Duration = timerDuration;
            accumulated = TimeSpan.Zero;
            stopwatch.Reset();
            stopwatch.Start();
            State = QuizTimerState.Running;
        }

        public void Pause()
        {
            if (State != QuizTimerState.Running && State != QuizTimerState.Overtime)
                return;

            accumulated += stopwatch.Elapsed;
            stopwatch.Reset();
            State = IsOvertime ? QuizTimerState.Overtime : QuizTimerState.Paused;
        }

        public void Resume()
        {
            if (State != QuizTimerState.Paused && State != QuizTimerState.Overtime)
                return;

            stopwatch.Start();
            State = IsOvertime ? QuizTimerState.Overtime : QuizTimerState.Running;
        }

        public void Stop()
        {
            if (stopwatch.IsRunning)
                accumulated += stopwatch.Elapsed;

            stopwatch.Reset();
            State = QuizTimerState.Completed;
        }

        public void RefreshState()
        {
            if ((State == QuizTimerState.Running || State == QuizTimerState.Overtime) && IsOvertime)
                State = QuizTimerState.Overtime;
        }

        public void Reset()
        {
            stopwatch.Reset();
            accumulated = TimeSpan.Zero;
            State = QuizTimerState.Stopped;
        }
    }
}
