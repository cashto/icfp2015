using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Text;
using System.Threading.Tasks;


static class Constants
{
    public const int GenerateGoalsMaxReturned = 5;
    public const int TargetLineBonus = 5;

    public const char West = 'p';
    public const char East = 'b';
    public const char Southwest = 'a';
    public const char Southeast = 'l';
    public const char Northwest = '6';
    public const char Northeast = '7';
    public const char Clockwise = 'd';
    public const char CounterClockwise = 'k';
    public const string ForwardMoves = "pbaldk";
    public const string ReverseMoves = "67dkpb";
}


class PriorityQueue<T>
{
    public PriorityQueue(Func<T, T, bool> lessFn)
    {
        this.lessFn = lessFn;
        this.items = new List<T>();
    }

    public bool isEmpty()
    {
        return !this.items.Any();
    }

    public void push(T obj)
    {
        items.Add(obj);
        var i = items.Count - 1;

        while (lessFn(items[i / 2], items[i]))
        {
            var j = i / 2;
            swap(i, j);
            i = j;
        }
    }

    public T pop()
    {
        var i = 0;
        swap(i, items.Count - 1);

        T obj = items.Last();
        items.RemoveAt(items.Count - 1);

        while (true)
        {
            var largest = i;

            if (i * 2 < items.Count && lessFn(items[largest], items[i * 2]))
            {
                largest = i * 2;
            }

            if (i * 2 + 1 < items.Count && lessFn(items[largest], items[i * 2 + 1]))
            {
                largest = i * 2 + 1;
            }

            if (i == largest)
            {
                return obj;
            }

            swap(i, largest);

            i = largest;
        }
    }

    private void swap(int i, int j)
    {
        T t = items[i];
        items[i] = items[j];
        items[j] = t;
    }

    private Func<T, T, bool> lessFn;
    private List<T> items;
}


public class Random
{
    public Random(UInt32 seed)
    {
        this.seed = seed;
    }

    public UInt32 next()
    {
        UInt32 ans = (seed >> 16) & 0x7FFF;
        seed = seed * multiplier + increment;
        return ans;
    }

    private UInt32 seed;

    private static UInt32 increment = 12345;
    private static UInt32 multiplier = 1103515245;
}


class GoalHeuristic : IComparable<GoalHeuristic>
{
    public GoalHeuristic(Board board)
    {
        this.score = board.score;

        Func<int, int, int> isOccupied = (i, j) =>
            i >= 0 && i < board.width &&
            j >= 0 && j < board.height &&
            board[i, j] ? 1 : 0;

        int uppermostNonemptyRow = board.height - 1;

        neighborCount = 0;
        for (var y = board.height - 1; y >= 0; --y)
        {
            bool isOddLine = (y & 1) != 0;
            for (var x = 0; x < board.width; ++x)
            {
                if (!board[x, y])
                {
                    continue;
                }

                uppermostNonemptyRow = y;

                if (y == 0)
                {
                    continue;
                }

                int nw = x - (isOddLine ? 0 : 1);
                int ne = x + (isOddLine ? 1 : 0);

                neighborCount +=
                    isOccupied(nw, y - 1) +
                    isOccupied(ne, y - 1) +
                    isOccupied(x - 1, y) +
                    isOccupied(x + 1, y);
            }
        }

        int targetRow = (uppermostNonemptyRow == board.height - 1) ? board.width : board.lineCount(uppermostNonemptyRow + 1);
        neighborCount += Constants.TargetLineBonus * targetRow;
    }

    public override string ToString()
    {
        return string.Format("{0}.{1}", score, neighborCount);
    }

    public int CompareTo(GoalHeuristic other)
    {
        // We care about things in this order:
        //    1. Maximize score.
        //    2a. Maximize (E, W, NE, NW) neighbor count (to encourage clumping).
        //    2b. Maximize the number of filled cells in the second-most-northmost row 
        //      (worth five neighbors per cell).

        if (this.score == other.score)
        {
            return other.neighborCount - this.neighborCount;
        }

        return other.score - this.score;
    }

    public int score;
    public int neighborCount;
}


class Board
{
    public Board(Input input)
    {
        this.score = 0;
        this.width = input.width;
        this.height = input.height;
        this.data = new bool[width * height];

        foreach (var cell in input.filled)
        {
            this[cell.x, cell.y] = true;
        }
    }

    public Board(Board parent, Unit piece)
    {
        this.width = parent.width;
        this.height = parent.height;
        this.data = parent.data.ToArray();

        foreach (var cell in piece.members)
        {
            this[cell.x, cell.y] = true;
        }

        linesRemoved = 0;
        for (var y = 0; y < height; ++y)
        {
            if (lineFull(y))
            {
                removeLine(y);
                ++linesRemoved;
            }
        }

        int points = piece.members.Count + 100 * (1 + linesRemoved) * linesRemoved / 2;
        int lineBonus = this.linesRemoved > 1 ? (this.linesRemoved - 1) * points / 10 : 0;
        this.score = parent.score + points + lineBonus;

        this.heuristicScore = new GoalHeuristic(this);
    }

    public bool contains(Unit unit)
    {
        foreach (var cell in unit.members)
        {
            bool contained = 
                cell.x >= 0 &&
                cell.y >= 0 &&
                cell.x < width &&
                cell.y < height &&
                !this[cell.x, + cell.y];
            
            if (!contained)
            {
                return false;
            }
        }

        return true;
    }

    public bool canLock(Unit piece)
    {
        return Constants.ForwardMoves.Any(c => !this.contains(piece.go(c)));
    }

    public bool this[int x, int y]
    {
        get
        {
            return this.data[x + y * width];
        }

        set
        {
            this.data[x + y * width] = value;
        }
    }

    public override string ToString()
    {
        var sb = new StringBuilder();

        sb.Append('\n');

        for (var y = 0; y < height; ++y)
        {
            if ((y & 1) != 0)
            {
                sb.Append(' ');
            }

            for (var x = 0; x < width; ++x)
            {
                sb.Append(this[x, y] ? '#' : '.');
                sb.Append(' ');
            }

            sb.Append('\n');
        }

        return sb.ToString();
    }

    private IEnumerable<bool> getLine(int y)
    {
        for (var x = 0; x < this.width; ++x)
        {
            yield return this[x, y];
        }
    }

    private bool lineFull(int y)
    {
        return this.getLine(y).All(i => i == true);
    }

    private bool lineEmpty(int y)
    {
        return this.getLine(y).All(i => i == false);
    }

    public int lineCount(int y)
    {
        return this.getLine(y).Count(i => i == true);
    }

    private void removeLine(int i)
    {
        for (var y = i; y >= 0; --y)
        {
            for (var x = 0; x < this.width; ++x)
            {
                this[x, y] = (y == 0 ? false : this[x, y - 1]);
            }
        }
    }

    private bool[] data;
    public int width;
    public int height;
    public int score;
    public int linesRemoved;
    public GoalHeuristic heuristicScore;
}


class BoardTree
{
    public BoardTree(Board board, string path = "", BoardTree parent = null)
    {
        this.parent = parent;
        this.board = board;
        this.path = path;
        this.disappointingChildren = 0;
        this.level = (parent == null ? 0 : parent.level + 1);
    }

    public override string ToString()
    {
        return string.Format("level={0}, score={1}, heuristic={2}", level, this.board.score, this.board.heuristicScore);
    }

    public BoardTree solve(List<Unit> pieces, TimeSpan timeout)
    {
        var currentPosition = this;
        var startTime = DateTime.UtcNow;

        while (currentPosition.level < pieces.Count &&
            DateTime.UtcNow - startTime < timeout)
        {
            currentPosition.walk(node => node.expand(pieces[node.level]));
            
            var bestNode = currentPosition.findHighestScoringLeafNode();
            if (bestNode != null)
            {
                currentPosition = bestNode;
            }
            else
            {
                // We've hit a dead end: time to backtrack.
                do
                {
                    if (currentPosition.parent == null)
                    {
                        // Can't backtrack any more. We've REALLY hit a dead end.
                        return findHighestScoringLeafNode();
                    }
                    
                    currentPosition = currentPosition.parent;
                    ++currentPosition.disappointingChildren;
                } while (currentPosition.children.Count < currentPosition.disappointingChildren);
            }
        }
        
        return findHighestScoringLeafNode();
    }

    private bool expand(Unit unit)
    {
        if (children != null)
        {
            return false;
        }

        if (!board.contains(unit))
        {
            return false;
        }

        //Console.WriteLine("Expanding {0}", this);

        this.children = generateGoals(unit)
            .Select(goal => generatePath(unit, goal))
            .Where(child => child != null)
            .Take(Constants.GenerateGoalsMaxReturned)
            .ToList();

        return this.children.Any();
    }

    private void walk(Action<BoardTree> fn)
    {
        var children = this.children;

        fn(this);

        if (children != null)
        {
            foreach (var child in children)
            {
                child.walk(fn);
            }
        }
    }

    private void walkFromRoot(Action<BoardTree> fn)
    {
        if (parent != null)
        {
            parent.walkFromRoot(fn);
        }

        fn(this);
    }

    public string getFullPath()
    {
        string ans = "";
        walkFromRoot(i => ans = ans + i.path);
        return ans;
    }

    private IEnumerable<Unit> generateGoals(Unit piece)
    {
        var ans = new List<Tuple<GoalHeuristic, Unit>>();

        for (var y = board.height - 1; y >= 0; --y)
        {
            for (var x = 0; x < board.width; ++x)
            {
                var destCell = new Cell(x, y);
                var rotatedPiece = piece;
                for (var i = 0; i < 6; ++i)
                {
                    var movedPiece = rotatedPiece.move(destCell - rotatedPiece.members.First());

                    if (board.contains(movedPiece) && board.canLock(movedPiece))
                    {
                        ans.Add(new Tuple<GoalHeuristic, Unit>(
                            new Board(board, movedPiece).heuristicScore, 
                            movedPiece));
                    }

                    rotatedPiece = rotatedPiece.rotate();
                }
            }
        }

        // ans = ans.OrderBy(i => i.Item1).ToList();
        // return ans.Select(i => i.Item2);

        return ans.OrderBy(i => i.Item1).Select(i => i.Item2);
    }

    public BoardTree generatePath(Unit start, Unit end)
    {
        var pq = new PriorityQueue<GeneratePathNode>((i, j) => i.score(start) > j.score(start));
        var set = new HashSet<Unit>();

        var rootNode = new GeneratePathNode(end);
        pq.push(rootNode);

        // Console.WriteLine("{0} -> {1}", end, start);

        while (!pq.isEmpty())
        {
            var item = pq.pop();
            var illegalSet = new HashSet<Unit>();
            item.getIllegalSet(illegalSet);

            // Console.WriteLine("{0}, score={1}", item, item.score(start));

            foreach (var c in Constants.ReverseMoves)
            {
                var newNode = new GeneratePathNode(item, c);
                if (newNode.Piece.Equals(start))
                {
                    char lockingMove = Constants.ForwardMoves.First(i => willLock(rootNode, i));
                    var ans = new BoardTree(
                        new Board(board, end),
                        reversePath(newNode.getPath().Reverse()) + lockingMove,
                        this);
                    ans.goal = end;
                    return ans;
                }

                if (board.contains(newNode.Piece) && !illegalSet.Contains(newNode.Piece))
                {
                    if (!set.Contains(newNode.Piece))
                    {
                        set.Add(newNode.Piece);
                        pq.push(newNode);
                    }
                }
            }
        }

        return null;
    }

    private bool willLock(GeneratePathNode node, char c)
    {
        var newNode = new GeneratePathNode(node, c);
        return !this.board.contains(newNode.Piece);
    }

    private static string reversePath(IEnumerable<char> s)
    {
        var sb = new StringBuilder();

        foreach (var c in s)
        {
            switch (c)
            {
                case 'p': sb.Append('b'); break;
                case 'b': sb.Append('p'); break;
                case 'a': sb.Append('7'); break;
                case 'l': sb.Append('6'); break;
                case 'd': sb.Append('k'); break;
                case 'k': sb.Append('d'); break;
                case '6': sb.Append('l'); break;
                case '7': sb.Append('a'); break;
            }
        }

        return sb.ToString();
    }

    private BoardTree findHighestScoringLeafNode()
    {
        BoardTree ans = null;
        var bestScore = int.MinValue;
        GoalHeuristic bestHeuristic = null;
        
        walk(i =>
        {
            if (i.children != null)
            {
                return;
            }

            if (i.board.score > bestScore ||
               (i.board.score == bestScore && i.board.heuristicScore.CompareTo(bestHeuristic) < 0))
            {
                bestScore = i.board.score;
                bestHeuristic = i.board.heuristicScore;
                ans = i;
            }
        });

        return ans;
    }
        

    class GeneratePathNode
    {
        public GeneratePathNode Parent { get; set; }
        public Unit Piece { get; set; }
        public Char Move { get; set; }
        public int Length { get; set; }

        public GeneratePathNode(Unit piece)
        {
            this.Piece = piece;
        }

        public GeneratePathNode(GeneratePathNode parent, char c)
        {
            this.Parent = parent;
            this.Move = c;
            this.Length = parent.Length + 1;
            this.Piece = parent.Piece.go(c);
        }

        public int score(Unit other)
        {
            var p1 = Piece.pivot;
            var p2 = other.pivot;
            return p1.distance(p2) + rotateDistance(other) + Length;
        }

        private int rotateDistance(Unit other)
        {
            var distance = (Piece.orientation + 6 - other.orientation) % Piece.symmetry;
            return distance <= Piece.symmetry / 2 ? distance : Piece.symmetry - distance;
        }

        public void getIllegalSet(HashSet<Unit> illegalSet)
        {
            illegalSet.Add(Piece);

            if (Parent != null)
            {
                Parent.getIllegalSet(illegalSet);
            }
        }

        public string getPath()
        {
            if (Parent == null)
            {
                return "";
            }

            return Parent.getPath() + this.Move;
        }

        public override string ToString()
        {
            return string.Format("piece={0}, path={1}", this.Piece, this.getPath());
        }
    }

    public Board board;
    public string path;

    private BoardTree parent;
    private List<BoardTree> children;
    private int disappointingChildren;
    private int level;
    private Unit goal;
}


class Cell : IEquatable<Cell>, IComparable<Cell>
{
    public Cell() { }
    public Cell(int x, int y) { this.x = x; this.y = y; }

    public int x { get; set; }
    public int y { get; set; }

    public Cell rotate(Cell pivot, bool clockwise = true)
    {
        int dir = clockwise ? 1 : -1;
        Func<int, int> odd = (a) => a & 1;
        var cell = Cell.zero + (this - pivot);
        int x = cell.x;
        int y = cell.y;
        int y2 = (dir * 4 * x + dir * 2 * odd(y) + 2 * y) >> 2;
        int x2 = (2 * x + odd(y) - dir * y * 3 - odd(y2) * 2) >> 2;
        return pivot + (new Cell(x2, y2) - Cell.zero);
    }

    public int distance(Cell other)
    {
        if (other.y < this.y)
        {
            return other.distance(this);
        }

        var dy = other.y - this.y;
        var sw = other + new Cell(-dy, dy);
        var se = other + new Cell(0, dy);

        return dy +
            (other.x < sw.x ? sw.x - other.x :
            other.x > se.x ? other.x - se.x :
            0);
    }

    public static Cell operator-(Cell lhs, Cell rhs)
    {
        return new Cell(
            (lhs.x - (lhs.y >> 1)) - (rhs.x - (rhs.y >> 1)), 
            lhs.y - rhs.y);
    }

    public static Cell operator+(Cell lhs, Cell rhs)
    {
        var y = lhs.y + rhs.y;
        return new Cell(
            lhs.x + rhs.x + (y >> 1) - (lhs.y >> 1),
            y);
    }

    public bool Equals(Cell other)
    {
        return this.x == other.x && this.y == other.y;
    }

    public int CompareTo(Cell other)
    {
        return this.y == other.y ?
            this.x - other.x :
            this.y - other.y;
    }

    public override string ToString()
    {
        return string.Format("{0},{1}", x, y);
    }

    public static readonly Cell zero = new Cell(0, 0);
}


class Unit : IEquatable<Unit>, ICloneable
{
    public List<Cell> members { get; set; }
    public Cell pivot { get; set; }
    public int orientation { get; private set; }

    public int symmetry
    {
        get
        {
            if (this.internalSymmetry < 0)
            {
                this.internalSymmetry = 6;
                Unit rotatedPiece = this;

                for (var i = 1; i < 4; ++i)
                {
                    rotatedPiece = rotatedPiece.rotate();
                    if (rotatedPiece.Equals(this))
                    {
                        this.internalSymmetry = i;
                        break;
                    }
                }
            }

            return this.internalSymmetry;
        }

        private set
        {
            internalSymmetry = value;
        }
    }

    private int internalSymmetry = -1;

    public object Clone()
    {
        return move(Cell.zero);
    }

    public bool Equals(Unit other)
    {
        return this.pivot.Equals(other.pivot) && this.members.All(i => other.members.Contains(i));
    }

    public Unit go(char direction)
    {
        switch (direction)
        {
            case Constants.Clockwise:
                return rotate(true);
            case Constants.CounterClockwise:
                return rotate(false);
            case Constants.West:
                return move(new Cell(-1, 0));
            case Constants.East:
                return move(new Cell(1, 0));
            case Constants.Southwest:
                return move(new Cell(-1, 1));
            case Constants.Southeast:
                return move(new Cell(0, 1));
            case Constants.Northwest:
                return move(new Cell(0, -1));
            case Constants.Northeast:
            default:
                return move(new Cell(1, -1));
        }
    }

    public Unit move(Cell direction)
    {
        return new Unit()
        {
            members = this.members.Select(i => i + direction).ToList(),
            pivot = this.pivot + direction,
            orientation = this.orientation,
            symmetry = this.symmetry
        };
    }

    public Unit rotate(bool clockwise = true)
    {
        return new Unit()
        {
            members = this.members.Select(i => i.rotate(pivot, clockwise)).ToList(),
            pivot = pivot,
            orientation = (orientation + (clockwise ? 1 : 5)) % 6,
            symmetry = this.symmetry
        };
    }

    public Unit center(int width)
    {
        int y_min = members.Min(i => i.y);
        int x_min = members.Min(i => i.x);
        int x_max = members.Max(i => i.x);
        int dx = (width - x_max + x_min - 1) >> 1;
        int dy = -y_min;

        Func<Cell, Cell> adjust = (cell) =>
            new Cell(cell.x + dx, cell.y + dy);
        
        return new Unit()
        {
            members = members.Select(adjust).ToList(),
            pivot = adjust(pivot),
            orientation = this.orientation,
            symmetry = this.symmetry
        };
    }

    public override string ToString()
    {
        members.Sort();
        return string.Join(",", members.Select(i => string.Format("[{0},{1}]", i.x, i.y))) +
            ":[" + pivot.x + "," + pivot.y + "]";
    }

    public override int GetHashCode()
    {
        int hash = 1;
        Action<int> addHash = (x) => { hash = hash * 1103515245 + x; };
        addHash(pivot.x);
        addHash(pivot.y);
        foreach (var cell in members)
        {
            addHash(cell.x);
            addHash(cell.y);
        }
        return hash;
    }
}


class Input
{
    public int id { get; set; }
    public List<Unit> units { get; set; }
    public int width { get; set; }
    public int height { get; set; }
    public List<Cell> filled { get; set; }
    public int sourceLength { get; set; }
    public List<UInt32> sourceSeeds { get; set; }
}


class Output
{
    public int problemId { get; set; }
    public UInt32 seed { get; set; }
    public string tag { get; set; }
    public string solution { get; set; }
}


class AnnotatedOutput
{
    public Output output { get; set; }
    public int score { get; set; }
}


class CommandLineParams
{
    public CommandLineParams(string[] args)
    {
        phrasesOfPower = new List<string>();
        megabyteLimit = int.MaxValue;
        timeLimit = TimeSpan.MaxValue;

        var argsEnum = args.Cast<string>().GetEnumerator();
        while (argsEnum.MoveNext())
        {
            switch (argsEnum.Current.ToLower())
            {
                case "-f":
                    argsEnum.MoveNext();
                    inputFilename = argsEnum.Current;
                    break;
                case "-t":
                    argsEnum.MoveNext();
                    timeLimit = TimeSpan.FromSeconds(double.Parse(argsEnum.Current));
                    break;
                case "-m":
                    argsEnum.MoveNext();
                    megabyteLimit = int.Parse(argsEnum.Current);
                    break;
                case "-p":
                    argsEnum.MoveNext();
                    phrasesOfPower.Add(argsEnum.Current);
                    break;
                case "-c":
                    argsEnum.MoveNext();
                    cores = int.Parse(argsEnum.Current);
                    break;
                
                // Nonstandard options:
                case "-r":
                    argsEnum.MoveNext();
                    randomSeed = UInt32.Parse(argsEnum.Current);
                    break;
                case "-s":
                    argsEnum.MoveNext();
                    movesToShow = argsEnum.Current;
                    break;
            }
        }
    }
    
    public string inputFilename { get; set; }
    public TimeSpan timeLimit { get; set; }
    public int megabyteLimit { get; set; }
    public List<string> phrasesOfPower { get; set; }
    public int cores { get; set; }
    public UInt32? randomSeed { get; set; }
    public string movesToShow { get; set; }
}


public static class Program
{
    public static IEnumerable<T> shuffle<T>(this IEnumerable<T> input_, Random rand)
    {
        var input = input_.ToList();
        while (input.Any())
        {
            int index = (int)(rand.next() % input.Count);
            yield return input[index];
            input.RemoveAt(index);
        }
    }

    static AnnotatedOutput solve(CommandLineParams commandLineParams, Input input, UInt32 seed)
    {
        var source = new List<Unit>();
        var rand = new Random(seed);
        for (var i = 0; i < input.sourceLength; ++i)
        {
            var piece = input.units[(int)(rand.next() % input.units.Count)];
            source.Add(piece.center(input.width));
        }

        var tree = new BoardTree(new Board(input));
        var ans = tree.solve(source, commandLineParams.timeLimit);
        string solution = ans.getFullPath();

        // show(input, seed, solution.Replace("/", ""));

        return new AnnotatedOutput()
        {
            score = ans.board.score,
            output = new Output()
            {
                problemId = input.id,
                solution = solution,
                seed = seed
            }
        };
    }

    static void show(
        Input input,
        UInt32 seedToShow,
        string movesToShow)
    {
        Console.WriteLine("Number of units: {0}", input.units.Count);
        Console.WriteLine("Number of sourceUnits: {0}", input.sourceLength);
        Console.WriteLine();

        var source = new List<Unit>();
        var rand = new Random(seedToShow);
        for (var i = 0; i < input.sourceLength; ++i)
        {
            source.Add(input.units[(int)(rand.next() % input.units.Count)]);
        }

        var board = new Board(input);
        Console.WriteLine(board);

        Unit piece = null;
        var sourceEnum = source.GetEnumerator();

        foreach (var c in movesToShow)
        {
            if (piece == null)
            {
                if (!sourceEnum.MoveNext())
                {
                    Console.WriteLine("----- Too many moves -----");
                    break;
                }

                piece = sourceEnum.Current.center(board.width);
            }

            var nextBoard = new Board(board, piece);
            Console.WriteLine(nextBoard);

            string friendlyStr = "";
            switch(c)
            {
                case Constants.West: friendlyStr = "W (<=)"; break;
                case Constants.East: friendlyStr = "E (=>)"; break;
                case Constants.Northwest: friendlyStr = "NW (^ <=)"; break;
                case Constants.Northeast: friendlyStr = "NE (^ =>)"; break;
                case Constants.Southwest: friendlyStr = "SW (v <=)"; break;
                case Constants.Southeast: friendlyStr = "SE (v =>)"; break;
                case Constants.Clockwise: friendlyStr = "Rotate"; break;
                case Constants.CounterClockwise: friendlyStr = "Rotate CCW"; break;
            }
            Console.WriteLine("----- Making move {0} -----", friendlyStr);

            var nextPiece = piece.go(c);
            if (!board.contains(nextPiece))
            {
                board = nextBoard;
                piece = null;
            }
            else
            {
                piece = nextPiece;
            }
        }
    }

    static void Main(string[] args)
    {
        var commandLineParams = new CommandLineParams(args);

        var input = JsonConvert.DeserializeObject<Input>(
            File.ReadAllText(commandLineParams.inputFilename));

        if (commandLineParams.movesToShow != null)
        {
            show(input, commandLineParams.randomSeed.Value, commandLineParams.movesToShow);
            return;
        }

        var output = input.sourceSeeds
            .Where(seed => !commandLineParams.randomSeed.HasValue || seed == commandLineParams.randomSeed.Value)
            .Select(seed => solve(commandLineParams, input, seed))
            .ToList();

        Console.WriteLine(JsonConvert.SerializeObject(output, Formatting.Indented, new JsonSerializerSettings() { NullValueHandling = NullValueHandling.Ignore }));
    }
}
