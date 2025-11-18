using System;
using System.Collections.Generic;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Graphics;
using Microsoft.Maui.Layouts;

namespace Flappy_Bird
{
    public partial class MainPage : ContentPage
    {
        double birdY;
        double velocity;
        double gravity = 0.25;
        double jumpStrength = -7;
        bool gameRunning = false;
        int score = 0;
        IDispatcherTimer gameTimer;
        double birdWidth = 0;
        double birdHeight = 0;
        bool birdSizeMeasured = false;
        readonly List<PipePair> pipes = new();
        Random rng = new();
        double pipeSpeed = 2.25;
        double timeSinceLastPipe = 0;
        double pipeSpawnInterval = 1500;
        int gapSize = 140;

        public MainPage()
        {
            InitializeComponent();

            birdY = 0;
            gameTimer = Dispatcher.CreateTimer();
            gameTimer.Interval = TimeSpan.FromMilliseconds(16);
            gameTimer.Tick += GameLoop;

            var tapGesture = new TapGestureRecognizer();
            tapGesture.Tapped += OnTapped;
            GameLayout.GestureRecognizers.Add(tapGesture);

            SizeChanged += OnPageSizeChanged;

            var playBtn = this.FindByName<Button>("PlayButton");
            if (playBtn != null)
                playBtn.Clicked += PlayButton_Clicked;

            var retryBtn = this.FindByName<Button>("RetryButton");
            if (retryBtn != null)
                retryBtn.Clicked += RetryButton_Clicked;
        }

        protected override void OnAppearing()
        {
            base.OnAppearing();

            this.FindByName<Button>("PlayButton")?.SetValue(Button.IsVisibleProperty, true);
            this.FindByName<Button>("RetryButton")?.SetValue(Button.IsVisibleProperty, false);
            this.FindByName<Label>("GameOverLabel")?.SetValue(Label.IsVisibleProperty, false);

            gameRunning = false;
            gameTimer.Stop();
        }

        void PlayButton_Clicked(object? sender, EventArgs e)
        {
            this.FindByName<Button>("PlayButton")?.SetValue(Button.IsVisibleProperty, false);
            StartGame();
        }

        void RetryButton_Clicked(object? sender, EventArgs e)
        {
            this.FindByName<Button>("RetryButton")?.SetValue(Button.IsVisibleProperty, false);
            this.FindByName<Label>("GameOverLabel")?.SetValue(Label.IsVisibleProperty, false);
            StartGame();
        }

        void OnPageSizeChanged(object? sender, EventArgs e)
        {
            if (GameLayout.Width <= 0 || GameLayout.Height <= 0)
                return;

            if (!birdSizeMeasured)
            {
                birdWidth = (Bird.WidthRequest > 0) ? Bird.WidthRequest : Bird.Measure(double.PositiveInfinity, double.PositiveInfinity).Request.Width;
                birdHeight = (Bird.HeightRequest > 0) ? Bird.HeightRequest : Bird.Measure(double.PositiveInfinity, double.PositiveInfinity).Request.Height;

                if (birdWidth <= 0) birdWidth = 34;
                if (birdHeight <= 0) birdHeight = 24;

                birdSizeMeasured = true;
            }

            if (birdY == 0)
            {
                birdY = GameLayout.Height / 2 - birdHeight / 2;
            }

            UpdateBirdPosition();
        }

        void UpdateBirdPosition()
        {
            AbsoluteLayout.SetLayoutFlags(Bird, AbsoluteLayoutFlags.None);
            AbsoluteLayout.SetLayoutBounds(Bird, new Rect(0.3 * GameLayout.Width, birdY, birdWidth, birdHeight));
        }

        void StartGame()
        {
            this.FindByName<Label>("GameOverLabel")?.SetValue(Label.IsVisibleProperty, false);
            this.FindByName<Button>("RetryButton")?.SetValue(Button.IsVisibleProperty, false);
            this.FindByName<Button>("PlayButton")?.SetValue(Button.IsVisibleProperty, false);

            if (GameLayout.Height <= 0)
            {
                GameLayout.SizeChanged += WaitForLayoutThenStart;
                return;
            }

            if (!birdSizeMeasured)
                OnPageSizeChanged(this, EventArgs.Empty);

            foreach (var p in pipes)
            {
                GameLayout.Children.Remove(p.Top);
                GameLayout.Children.Remove(p.Bottom);
            }
            pipes.Clear();

            birdY = GameLayout.Height / 2 - birdHeight / 2;
            velocity = 0;
            score = 0;
            UpdateScoreImages();
            gameRunning = true;
            timeSinceLastPipe = 0;
            gameTimer.Start();
        }

        void WaitForLayoutThenStart(object? s, EventArgs e)
        {
            if (GameLayout.Height <= 0) return;
            GameLayout.SizeChanged -= WaitForLayoutThenStart;
            StartGame();
        }

        void GameLoop(object? sender, EventArgs e)
        {
            if (!gameRunning) return;

            timeSinceLastPipe += gameTimer.Interval.TotalMilliseconds;
            if (timeSinceLastPipe >= pipeSpawnInterval)
            {
                timeSinceLastPipe = 0;
                SpawnPipe();
            }

            velocity += gravity;
            birdY += velocity;

            if (birdY < 0)
            {
                birdY = 0;
                velocity = 0;
            }
            else if (birdY > GameLayout.Height - birdHeight)
            {
                birdY = GameLayout.Height - birdHeight;
                GameOver();
            }

            UpdateBirdPosition();

            for (int i = pipes.Count - 1; i >= 0; i--)
            {
                var p = pipes[i];
                p.X -= pipeSpeed;
                p.UpdateLayout();

                double birdX = 0.3 * GameLayout.Width;
                if (!p.Passed && (p.X + p.Width) < birdX)
                {
                    p.Passed = true;
                    score++;
                    UpdateScoreImages();
                }

                var birdRect = new Rect(birdX, birdY, birdWidth, birdHeight);
                if (RectsIntersect(birdRect, p.TopRect) || RectsIntersect(birdRect, p.BottomRect))
                {
                    GameOver();
                    return;
                }

                if (p.X + p.Width < -10)
                {
                    GameLayout.Children.Remove(p.Top);
                    GameLayout.Children.Remove(p.Bottom);
                    pipes.RemoveAt(i);
                }
            }
        }

        void OnTapped(object? sender, TappedEventArgs e)
        {
            if (!gameRunning) return;
            velocity = jumpStrength;
        }

        void GameOver()
        {
            if (!gameRunning) return;
            gameRunning = false;
            gameTimer.Stop();

            this.FindByName<Label>("GameOverLabel")?.SetValue(Label.IsVisibleProperty, true);
            this.FindByName<Button>("RetryButton")?.SetValue(Button.IsVisibleProperty, true);
        }

        bool RectsIntersect(Rect a, Rect b)
        {
            return !(a.Right <= b.Left || a.Left >= b.Right || a.Bottom <= b.Top || a.Top >= b.Bottom);
        }

        void SpawnPipe()
        {
            if (GameLayout.Width <= 0 || GameLayout.Height <= 0) return;

            double pipeWidth = Math.Max(48, GameLayout.Width * 0.12);
            double pipeHeight = GameLayout.Height;

            double minGapY = GameLayout.Height * 0.18;
            double maxGapY = GameLayout.Height * 0.72;
            double gapCenter = rng.NextDouble() * (maxGapY - minGapY) + minGapY;

            double gapTop = gapCenter - (gapSize / 2.0);
            double gapBottom = gapCenter + (gapSize / 2.0);

            double spawnX = GameLayout.Width + pipeWidth;

            var top = new Image
            {
                Source = "pipe_green.png",
                Rotation = 180,
                Aspect = Aspect.Fill,
                WidthRequest = pipeWidth,
                HeightRequest = pipeHeight
            };

            var bottom = new Image
            {
                Source = "pipe_green.png",
                Aspect = Aspect.Fill,
                WidthRequest = pipeWidth,
                HeightRequest = pipeHeight
            };

            var pair = new PipePair(top, bottom, spawnX, pipeWidth, pipeHeight, gapTop, gapBottom);

            int birdIndex = GameLayout.Children.IndexOf(Bird);
            if (birdIndex < 0) GameLayout.Children.Add(top);
            else GameLayout.Children.Insert(birdIndex, top);

            if (birdIndex < 0) GameLayout.Children.Add(bottom);
            else GameLayout.Children.Insert(birdIndex, bottom);

            pair.UpdateLayout();
            pipes.Add(pair);
        }

        void UpdateScoreImages()
        {
            var container = this.FindByName<StackLayout>("ScoreContainer");
            container?.Children.Clear();

            var digits = (score == 0) ? new[] { '0' } : score.ToString().ToCharArray();

            if (container == null) return;

            foreach (var d in digits)
            {
                var img = new Image
                {
                    Source = GetDigitImageName(d),
                    Aspect = Aspect.AspectFit,
                    HeightRequest = 48,
                    WidthRequest = 32
                };

                container.Children.Add(img);
            }
        }

        static string GetDigitImageName(char digit) => digit switch
        {
            '0' => "zero.png",
            '1' => "one.png",
            '2' => "two.png",
            '3' => "three.png",
            '4' => "four.png",
            '5' => "five.png",
            '6' => "six.png",
            '7' => "seven.png",
            '8' => "eight.png",
            '9' => "nine.png",
            _ => "zero.png"
        };

        class PipePair
        {
            public Image Top { get; }
            public Image Bottom { get; }
            public double X { get; set; }
            public double Width { get; }
            public double Height { get; }
            double gapTop;
            double gapBottom;

            public bool Passed { get; set; }

            public PipePair(Image top, Image bottom, double startX, double width, double height, double gapTop, double gapBottom)
            {
                Top = top;
                Bottom = bottom;
                X = startX;
                Width = width;
                Height = height;
                this.gapTop = gapTop;
                this.gapBottom = gapBottom;
            }

            public void UpdateLayout()
            {
                AbsoluteLayout.SetLayoutFlags(Top, AbsoluteLayoutFlags.None);
                AbsoluteLayout.SetLayoutBounds(Top, new Rect(X, gapTop - Height, Width, Height));

                AbsoluteLayout.SetLayoutFlags(Bottom, AbsoluteLayoutFlags.None);
                AbsoluteLayout.SetLayoutBounds(Bottom, new Rect(X, gapBottom, Width, Height));
            }

            public Rect TopRect => new Rect(X, gapTop - Height, Width, Height);
            public Rect BottomRect => new Rect(X, gapBottom, Width, Height);
        }
    }
}

