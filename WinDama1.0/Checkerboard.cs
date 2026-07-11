using System;

namespace CheckersGame
{
    public class Checkerboard
    {
        // 64-bit board representation (2 bits per square)
        private ulong bitboard;

        // 32-bit mask to indicate if a square is occupied
        private uint occupancyMask;

        // Enum to represent the piece types
        public enum PieceType
        {
            Empty = 0b00,
            WhitePiece = 0b00,
            WhiteKing = 0b01,
            BlackPiece = 0b10,
            BlackKing = 0b11
        }

        // Constructor initializes the board
        public Checkerboard()
        {
            InitializeBoard();
        }

        // Initialize the board with the starting position
        private void InitializeBoard()
        {
            bitboard = 0;
            occupancyMask = 0;

            // Set up the white pieces
            for (int i = 0; i < 12; i++) // First 12 squares for white pieces
            {
                SetPiece(i, PieceType.WhitePiece);
            }

            // Set up the black pieces
            for (int i = 20; i < 32; i++) // Last 12 squares for black pieces
            {
                SetPiece(i, PieceType.BlackPiece);
            }
        }

        // Method to set a piece on the board
        public void SetPiece(int squareIndex, PieceType piece)
        {
            if (squareIndex < 0 || squareIndex >= 32)
            {
                throw new ArgumentOutOfRangeException(nameof(squareIndex), "Invalid square index.");
            }

            // Clear existing piece at squareIndex
            ClearPiece(squareIndex);

            // Set the new piece
            ulong pieceValue = (ulong)piece;
            bitboard |= (pieceValue << (squareIndex * 2));

            // Update the occupancy mask
            occupancyMask |= (1u << squareIndex);
        }

        // Method to get a piece at a specific square
        public PieceType GetPiece(int squareIndex)
        {
            if (squareIndex < 0 || squareIndex >= 32)
            {
                throw new ArgumentOutOfRangeException(nameof(squareIndex), "Invalid square index.");
            }

            // Check if the square is occupied
            if ((occupancyMask & (1u << squareIndex)) == 0)
            {
                return PieceType.Empty;
            }

            // Get the piece value
            ulong pieceValue = (bitboard >> (squareIndex * 2)) & 0b11;
            return (PieceType)pieceValue;
        }

        // Method to clear a piece from a square
        public void ClearPiece(int squareIndex)
        {
            if (squareIndex < 0 || squareIndex >= 32)
            {
                throw new ArgumentOutOfRangeException(nameof(squareIndex), "Invalid square index.");
            }

            // Clear the piece from the bitboard
            ulong mask = ~(0b11ul << (squareIndex * 2));
            bitboard &= mask;

            // Update the occupancy mask
            occupancyMask &= ~(1u << squareIndex);
        }

        // Method to move a piece from one square to another
        public void MovePiece(int fromSquare, int toSquare)
        {
            if (fromSquare < 0 || fromSquare >= 32 || toSquare < 0 || toSquare >= 32)
            {
                throw new ArgumentOutOfRangeException("Invalid square index.");
            }

            PieceType piece = GetPiece(fromSquare);
            ClearPiece(fromSquare);
            SetPiece(toSquare, piece);
        }

        // Method to display the board (for debugging)
        public void DisplayBoard()
        {
            for (int i = 0; i < 32; i++)
            {
                if (i % 4 == 0 && i != 0)
                {
                    Console.WriteLine();
                }

                PieceType piece = GetPiece(i);
                Console.Write($"{(int)piece:X2} ");
            }
            Console.WriteLine();
        }
    }
}
