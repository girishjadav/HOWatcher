using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace VOWatcherWPFApp
{
    public partial class TimePicker : UserControl
    {
        public TimePicker()
        {
            InitializeComponent();
            SetTime(Time);
        }

        public static readonly DependencyProperty TimeProperty =
            DependencyProperty.Register("Time", typeof(TimeSpan), typeof(TimePicker),
                new FrameworkPropertyMetadata(default(TimeSpan), FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnTimeChanged));

        public TimeSpan Time
        {
            get => (TimeSpan)GetValue(TimeProperty);
            set => SetValue(TimeProperty, value);
        }

        private static void OnTimeChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is TimePicker tp && e.NewValue is TimeSpan time)
            {
                tp.SetTime(time);
            }
        }

        private void SetTime(TimeSpan time)
        {
            HourBox.Text = time.Hours.ToString("D2");
            MinuteBox.Text = time.Minutes.ToString("D2");
            SecondBox.Text = time.Seconds.ToString("D2");
        }

        private void TimePartChanged(object sender, TextChangedEventArgs e)
        {
            if (int.TryParse(HourBox.Text, out int h) &&
                int.TryParse(MinuteBox.Text, out int m) &&
                int.TryParse(SecondBox.Text, out int s))
            {
                Time = new TimeSpan(h % 24, m % 60, s % 60);
            }
        }

        private void NumberOnly(object sender, TextCompositionEventArgs e)
        {
            e.Handled = !int.TryParse(e.Text, out _);
        }
    }
}
