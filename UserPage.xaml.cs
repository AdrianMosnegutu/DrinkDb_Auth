using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;
using System.Collections.Generic;

namespace DrinkDb_Auth
{
    public sealed partial class UserPage : Page
    {
        public UserPage()
        {
            this.InitializeComponent();
            LoadMockUserData();
        }

        private void LoadMockUserData()
        {
            // Create a mock user with dummy data
            var mockUser = new UserModel
            {
                Name = "Serginio",
                Username = "sergio24",
                Status = "Online",
                Reviews = new List<ReviewModel>
                {
                    new ReviewModel { Date = DateTime.Now.AddDays(-2), Rating = 4, Comment = "Really good taste!" },
                    new ReviewModel { Date = DateTime.Now.AddDays(-10), Rating = 2, Comment = "Could be better" }
                },
                Drinklist = new List<string>
                {
                    "Coca Cola Zero",
                    "Pepsi Twist",
                    "Lemonade"
                }
            };

            // Display basic info
            NameTextBlock.Text = mockUser.Name;
            UsernameTextBlock.Text = "@" + mockUser.Username;
            StatusTextBlock.Text = $"Status: {mockUser.Status}";

            // Display reviews with stars
            foreach (var review in mockUser.Reviews)
            {
                var reviewPanel = new StackPanel { Orientation = Orientation.Vertical, Spacing = 4 };

                // Create a stars string: filled star "★", empty star "☆"
                string stars = new string('★', review.Rating) + new string('☆', 5 - review.Rating);

                // Star rating display
                var starsText = new TextBlock
                {
                    Text = stars,
                    FontSize = 20,
                    Foreground = new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.Gold)
                };
                reviewPanel.Children.Add(starsText);

                // Date display
                var dateText = new TextBlock
                {
                    Text = review.Date.ToShortDateString(),
                    FontSize = 12,
                    Foreground = new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.Gray)
                };
                reviewPanel.Children.Add(dateText);

                // Comment display
                var commentText = new TextBlock
                {
                    Text = review.Comment,
                    FontSize = 14
                };
                reviewPanel.Children.Add(commentText);

                ReviewsItemsControl.Items.Add(reviewPanel);
            }

            // Display drinklist items
            foreach (var drink in mockUser.Drinklist)
            {
                var drinkText = new TextBlock
                {
                    Text = drink,
                    FontSize = 16
                };
                DrinklistItemsControl.Items.Add(drinkText);
            }
        }

        private void EditAccountButton_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new ContentDialog
            {
                Title = "Edit Account",
                Content = "Account editing is not implemented yet.",
                CloseButtonText = "OK",
                XamlRoot = this.Content.XamlRoot
            };

            _ = dialog.ShowAsync();
        }
    }

    // Simple user model for mock data
    public class UserModel
    {
        public string Name { get; set; }
        public string Username { get; set; }
        public string Status { get; set; }
        public List<ReviewModel> Reviews { get; set; } = new List<ReviewModel>();
        public List<string> Drinklist { get; set; } = new List<string>();
    }

    // Simple review model for mock data
    public class ReviewModel
    {
        public DateTime Date { get; set; }
        public int Rating { get; set; }
        public string Comment { get; set; }
    }
}
