document.addEventListener("DOMContentLoaded", function () {
    const cells = document.querySelectorAll(".chessboard-cell");

    cells.forEach(cell => {
        cell.addEventListener("click", function () {
            const row = this.dataset.row;
            const col = this.dataset.col;
            DotNet.invokeMethodAsync("ChessGame", "OnCellClick", row, col);
        });
    });
});
