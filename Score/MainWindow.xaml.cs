using System;
using System.Linq;
using System.Windows;

namespace Score
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
            
            var countT1 = score.ScoreCounts.Where(m => m.Team == "JBA 1").Count();
            lblScoreTeam1.Content= score.ScoreCounts.OrderBy(c => 1 == 1).Skip(countT1 - 1).FirstOrDefault().ScoreValue;

            var countT2 = score.ScoreCounts.Where(m => m.Team == "JBA 2").Count();
            lblScoreTeam2.Content = score.ScoreCounts.OrderBy(c => 1 == 1).Skip(countT2 - 1).FirstOrDefault().ScoreValue;

            var countT3 = score.ScoreCounts.Where(m => m.Team == "YVM 1").Count();
            lblScoreTeam3.Content = score.ScoreCounts.OrderBy(c => 1 == 1).Skip(countT3 - 1).FirstOrDefault().ScoreValue;

            var countT4 = score.ScoreCounts.Where(m => m.Team == "YVM 2").Count();
            lblScoreTeam4.Content = score.ScoreCounts.OrderBy(c => 1 == 1).Skip(countT4 - 1).FirstOrDefault().ScoreValue;
        }
        ScoreDbContext score = new ScoreDbContext();
        private void Button_Click(object sender, RoutedEventArgs e)
        {
            //var count = score.ScoreCounts.Where(m => m.Team == "JBA 1").Count();
            // int p = score.ScoreCounts.OrderBy(c => 1 == 1).Skip(count - 1).FirstOrDefault().ScoreValue;
            var m = from x in score.ScoreCounts
                    where x.Team == "JBA 1"
                    select x.ScoreValue;

            int p = m.ToList().LastOrDefault();
                   

            ScoreCount sc = new ScoreCount();
            sc.Team = "JBA 1";
            sc.ScoreValue = p + 1;
            lblScoreTeam1.Content = sc.ScoreValue;
            score.ScoreCounts.Add(sc);
            score.SaveChanges();
        }

        private void Button_Click_1(object sender, RoutedEventArgs e)
        {
            //var count = score.ScoreCounts.Where(m => m.Team == "JBA 1").Count();
            //int p = score.ScoreCounts.OrderBy(c => 1 == 1).Skip(count - 1).FirstOrDefault().ScoreValue;
            var m = from x in score.ScoreCounts
                    where x.Team == "JBA 1"
                    select x.ScoreValue;

            int p = m.ToList().LastOrDefault();

            ScoreCount sc = new ScoreCount();
            sc.Team = "JBA 1";
            sc.ScoreValue = p - 1;
            lblScoreTeam1.Content = sc.ScoreValue;
            score.ScoreCounts.Add(sc);
            score.SaveChanges();
           
        }

        private void Button_Click_2(object sender, RoutedEventArgs e)
        {
            // var count = score.ScoreCounts.Where(m => m.Team == "JBA 2").Count();
            //int p = score.ScoreCounts.OrderBy(c => 1 == 1).Skip(count - 1).FirstOrDefault().ScoreValue;
            var m = from x in score.ScoreCounts
                    where x.Team == "JBA 2"
                    select x.ScoreValue;

            int p = m.ToList().LastOrDefault();
            ScoreCount sc = new ScoreCount();
            sc.Team = "JBA 2";
            sc.ScoreValue = p - 1;
            lblScoreTeam2.Content = sc.ScoreValue;
            score.ScoreCounts.Add(sc);
            score.SaveChanges();
        }

        private void Button_Click_3(object sender, RoutedEventArgs e)
        {
            //var count = score.ScoreCounts.Where(m => m.Team == "JBA 2").Count();
            //int p = score.ScoreCounts.OrderBy(c => 1 == 1).Skip(count - 1).FirstOrDefault().ScoreValue;
            var m = from x in score.ScoreCounts
                    where x.Team == "JBA 2"
                    select x.ScoreValue;

            int p = m.ToList().LastOrDefault();
            ScoreCount sc = new ScoreCount();
            sc.Team = "JBA 2";
            sc.ScoreValue = p + 1;
            lblScoreTeam2.Content = sc.ScoreValue;
            score.ScoreCounts.Add(sc);
            score.SaveChanges();
        }

        private void Button_Click_4(object sender, RoutedEventArgs e)
        {
            //var count = score.ScoreCounts.Where(m => m.Team == "YVM 1").Count();
            // int p = score.ScoreCounts.OrderBy(c => 1 == 1).Skip(count - 1).FirstOrDefault().ScoreValue;
            var m = from x in score.ScoreCounts
                    where x.Team == "YVM 1"
                    select x.ScoreValue;

            int p = m.ToList().LastOrDefault();
            ScoreCount sc = new ScoreCount();
            sc.Team = "YVM 1";
            sc.ScoreValue = p - 1;
            lblScoreTeam3.Content = sc.ScoreValue;
            score.ScoreCounts.Add(sc);
            score.SaveChanges();
        }

        private void Button_Click_5(object sender, RoutedEventArgs e)
        {
            //  var count = score.ScoreCounts.Where(m => m.Team == "YVM 1").Count();
            // int p = score.ScoreCounts.OrderBy(c => 1 == 1).Skip(count - 1).FirstOrDefault().ScoreValue;
            var m = from x in score.ScoreCounts
                    where x.Team == "YVM 1"
                    select x.ScoreValue;

            int p = m.ToList().LastOrDefault();
            ScoreCount sc = new ScoreCount();
            sc.Team = "YVM 1";
            sc.ScoreValue = p + 1;
            lblScoreTeam3.Content = sc.ScoreValue;
            score.ScoreCounts.Add(sc);
            score.SaveChanges();

        }

        private void Button_Click_6(object sender, RoutedEventArgs e)
        {
            //   var count = score.ScoreCounts.Where(m => m.Team == "YVM 2").Count();
            //int p = score.ScoreCounts.OrderBy(c => 1 == 1).Skip(count - 1).FirstOrDefault().ScoreValue;
            var m = from x in score.ScoreCounts
                    where x.Team == "YVM 2"
                    select x.ScoreValue;

            int p = m.ToList().LastOrDefault();
            ScoreCount sc = new ScoreCount();
            sc.Team = "YVM 2";
            sc.ScoreValue = p - 1;
            lblScoreTeam4.Content = sc.ScoreValue;
            score.ScoreCounts.Add(sc);
            score.SaveChanges();

        }

        private void Button_Click_7(object sender, RoutedEventArgs e)
        {
            //var count = score.ScoreCounts.Where(m => m.Team == "YVM 2").Count();
            // int p = score.ScoreCounts.OrderBy(c => 1 == 1).Skip(count - 1).FirstOrDefault().ScoreValue;
            var m = from x in score.ScoreCounts
                    where x.Team == "YVM 2"
                    select x.ScoreValue;

            int p = m.ToList().LastOrDefault();
            ScoreCount sc = new ScoreCount();
            sc.Team = "YVM 2";
            sc.ScoreValue = p + 1;
            lblScoreTeam4.Content = sc.ScoreValue;
            score.ScoreCounts.Add(sc);
            score.SaveChanges();

        }
    }
}
