using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.SignalR.Client;
using MongoDB.Driver;
using StackExchange.Redis;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace ChessGame.Components.Pages
{
    public partial class ChessGame : ComponentBase
    {
        private string[,] board = new string[8, 8];
        private bool isWhiteTurn = true;
        private HubConnection hubConnection;
        private IMongoCollection<GameState> gameStateCollection;
        private IDatabase redisDatabase;

        protected override async Task OnInitializedAsync()
        {
            InitializeBoard();
            await InitializeSignalR();
            InitializeMongoDB();
            InitializeRedis();
        }

        private void InitializeBoard()
        {
            // Initialize the board with pieces
            board[0, 0] = "R"; // Rook
            board[0, 1] = "N"; // Knight
            board[0, 2] = "B"; // Bishop
            board[0, 3] = "Q"; // Queen
            board[0, 4] = "K"; // King
            board[0, 5] = "B"; // Bishop
            board[0, 6] = "N"; // Knight
            board[0, 7] = "R"; // Rook

            for (int i = 0; i < 8; i++)
            {
                board[1, i] = "P"; // Pawn
                board[6, i] = "p"; // Pawn
            }

            board[7, 0] = "r"; // Rook
            board[7, 1] = "n"; // Knight
            board[7, 2] = "b"; // Bishop
            board[7, 3] = "q"; // Queen
            board[7, 4] = "k"; // King
            board[7, 5] = "b"; // Bishop
            board[7, 6] = "n"; // Knight
            board[7, 7] = "r"; // Rook
        }

        private async Task InitializeSignalR()
        {
            hubConnection = new HubConnectionBuilder()
                .WithUrl("https://localhost:5001/chessHub")
                .Build();

            hubConnection.On<string, string>("ReceiveMove", (from, to) =>
            {
                // Handle received move
            });

            await hubConnection.StartAsync();
        }

        private void InitializeMongoDB()
        {
            var client = new MongoClient("mongodb://localhost:27017");
            var database = client.GetDatabase("ChessGame");
            gameStateCollection = database.GetCollection<GameState>("GameState");
        }

        private void InitializeRedis()
        {
            var redis = ConnectionMultiplexer.Connect("localhost:6379");
            redisDatabase = redis.GetDatabase();
        }

        private string GetCellClass(int row, int col)
        {
            return (row + col) % 2 == 0 ? "light-cell" : "dark-cell";
        }

        private string GetPiece(int row, int col)
        {
            return board[row, col];
        }

        private void OnCellClick(int row, int col)
        {
            // Handle cell click event
        }

        private class GameState
        {
            public string Id { get; set; }
            public string[,] Board { get; set; }
            public bool IsWhiteTurn { get; set; }
        }
    }
}
