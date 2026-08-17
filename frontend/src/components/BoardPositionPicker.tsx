import type { ChallengeTaskDto } from "../api/challengesApi";

interface BoardPositionPickerProps {
  tasks: ChallengeTaskDto[];
  value: { boardX: number; boardY: number };
  editingTaskId?: string;
  onChange: (position: { boardX: number; boardY: number }) => void;
}

export function BoardPositionPicker({ tasks, value, editingTaskId, onChange }: BoardPositionPickerProps) {
  function isOccupied(x: number, y: number) {
    return tasks.some((task) => task.boardX === x && task.boardY === y && task.id !== editingTaskId);
  }

  return (
    <div className="board-picker" aria-label="棋盘位置选择器">
      {Array.from({ length: 64 }, (_, index) => {
        const x = index % 8;
        const y = 7 - Math.floor(index / 8);
        const occupied = isOccupied(x, y);
        const selected = value.boardX === x && value.boardY === y;

        return (
          <button
            className={`board-picker-cell ${(x + y) % 2 === 0 ? "light" : "dark"} ${selected ? "selected" : ""} ${occupied ? "occupied" : ""}`}
            disabled={occupied}
            key={`${x}:${y}`}
            type="button"
            onClick={() => onChange({ boardX: x, boardY: y })}
            title={occupied ? `(${x}, ${y}) 已占用` : `选择 (${x}, ${y})`}
          >
            <span>{selected ? "●" : occupied ? "×" : ""}</span>
          </button>
        );
      })}
    </div>
  );
}
