using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Text;
using System.Threading.Tasks;


static class Constants
{
    public const int GenerateGoalsMinExamined = 100;
    public const int GenerateGoalsMaxReturned = 20;
    public const int PruneTreeLimit = 20;

    public const char West = 'p';
    public const char East = 'b';
    public const char Southwest = 'a';
    public const char Southeast = 'l';
    public const char Northwest = '6';
    public const char Northeast = '7';
    public const char Clockwise = 'd';
    public const char CounterClockwise = 'k';
    public const string ForwardMoves = "pbaldk";
    public const string ReverseMoves = "pb67dk";
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
        if (items.Count > 100000)
        {
            return false;
        }

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


class Board
{
    public Board(int width, int height)
    {
        this.score = 0;
        this.width = width;
        this.height = height;
        this.data = new bool[width * height];
    }

    private Board(Board other, int score)
    {
        this.score = score;
        this.width = other.width;
        this.height = other.height;
        this.data = other.data.ToArray();
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

    public Board place(Unit piece)
    {
        Board ans = new Board(this, this.score + piece.members.Count);

        // TODO drop lines
        // TODO calculate score
        foreach (var cell in piece.members)
        {
            ans.data[cell.x + cell.y * width] = true;
        }

        return ans;
    }

    public bool this[int x, int y]
    {
        get
        {
            return this.data[x + y * width];
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

    private bool[] data;
    public int width;
    public int height;
    public int score;
}


class BoardTree : IComparable<BoardTree>
{
    public BoardTree(Board board, string path = "", BoardTree parent = null)
    {
        this.parent = parent;
        this.board = board;
        this.path = path;
        
        this.heuristicScore = 0;
        for (var y = 0; y < board.height; y++)
        {
            for (var x = 0; x < board.width; x++)
            {
                if (!board[x, y])
                {
                    heuristicScore += board.height - y;
                }
            }
        }
    }

    public int CompareTo(BoardTree other)
    {
        return other.heuristicScore - this.heuristicScore;
    }

    public override string ToString()
    {
        int nodes = 0;
        walk(i => ++nodes);
        return string.Format("path={0}, heuristicScore={1}, nodes={2}", this.path, this.heuristicScore, nodes);
    }

    public bool expand(Unit unit)
    {
        if (children != null)
        {
            return false;
        }

        if (!board.contains(unit))
        {
            return false;
        }

        var goals = generateGoals(unit);
        this.children = goals
            .Select(goal => generatePath(unit, goal))
            .Where(child => child != null)
            .ToList();

        return true;
    }

    public void walk(Action<BoardTree> fn)
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

    public void walkFromRoot(Action<BoardTree> fn)
    {
        if (parent != null)
        {
            parent.walkFromRoot(fn);
        }

        fn(this);
    }

    public IEnumerable<BoardTree> getBestLeafNodes()
    {
        var allNodes = new List<BoardTree>();
        walk(i => 
            {
                if (i.children == null)
                {
                    allNodes.Add(i);
                }
            });

        allNodes.Sort();
        return allNodes;
    }

    public void prune(int n)
    {
        walk(i => i.mark = true);

        foreach (var node in getBestLeafNodes().Take(n))
        {
            node.walkFromRoot(i => i.mark = false);
        }

        walk(boardTree => 
            {
                if (boardTree.children != null)
                {
                    boardTree.children = boardTree.children.Where(child => !child.mark).ToList();
                }
            });
    }

    private IEnumerable<Unit> generateGoals(Unit piece)
    {
        var ans = new List<Unit>();

        for (var y = board.height - 1; y >= 0; --y)
        {
            for (var x = 0; x < board.width; ++x)
            {
                var rotatedPiece = piece;
                for (var i = 0; i < 6; ++i)
                {
                    var cell = rotatedPiece.members.First();
                    var movedPiece = rotatedPiece.move(new Cell(x, y) - cell);
                    
                    if (board.contains(movedPiece) && !ans.Contains(movedPiece) && board.canLock(movedPiece))
                    {
                        ans.Add(movedPiece);
                    }

                    rotatedPiece = rotatedPiece.rotate();
                }
            }

            if (ans.Count > Constants.GenerateGoalsMinExamined)
            {
                break;
            }
        }

        return ans.shuffle(new Random((UInt32)DateTime.UtcNow.Ticks)).Take(Constants.GenerateGoalsMaxReturned);
    }

    public BoardTree generatePath(Unit start, Unit end)
    {
        var pq = new PriorityQueue<GeneratePathNode>((i, j) => i.score(start) > j.score(start));
        var rootNode = new GeneratePathNode(end);
        pq.push(rootNode);

        while (!pq.isEmpty())
        {
            var item = pq.pop();

            foreach (var c in Constants.ReverseMoves)
            {
                var newNode = new GeneratePathNode(item, c);
                if (newNode.Piece.Equals(start))
                {
                    char lockingMove = Constants.ForwardMoves.First(i => willLock(rootNode, i));
                    return new BoardTree(
                        board.place(end),
                        reversePath(newNode.getPath()) + lockingMove,
                        this);
                }

                if (item.isLegal(newNode))
                {
                    pq.push(newNode);
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

    private static string reversePath(string s)
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
            // TODO distance between hex points isn't manhattan distance
            Cell pivotDistance = Piece.pivot - other.pivot;
            return Math.Abs(pivotDistance.x) + Math.Abs(pivotDistance.y) + rotateDistance(other.move(pivotDistance)) + Length;
        }

        private int rotateDistance(Unit other)
        {
            if (other.Equals(Piece))
            {
                return 0;
            }

            var rotateCw = other.rotate(true);
            if (rotateCw.Equals(Piece))
            {
                return 1;
            }

            var rotateCcw = other.rotate(false);
            if (rotateCcw.Equals(Piece))
            {
                return 1;
            }

            if (rotateCw.rotate(true).Equals(Piece))
            {
                return 2;
            }

            if (rotateCcw.rotate(false).Equals(Piece))
            {
                return 2;
            }

            return 3;
        }

        public bool isLegal(GeneratePathNode node)
        {
            return !Piece.Equals(node.Piece) && (Parent == null || Parent.isLegal(node));
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
    private bool mark;
    private int heuristicScore;
}


class Cell : IEquatable<Cell>
{
    public Cell() { }
    public Cell(int x, int y) { this.x = x; this.y = y; }

    public int x { get; set; }
    public int y { get; set; }

    public Cell rotate(bool clockwise = true)
    {
        int dir = clockwise ? 1 : -1;
        Func<int, int> odd = (a) => a & 1;
        int y2 = (dir * 4 * x + dir * 2 * odd(y) + 2 * y) / 4;
        int x2 = (2 * x + odd(y) - dir * y * 3 - odd(y2) * 2) / 4;
        return new Cell(x2, y2);
    }

    public static int distance(Cell other)
    {
        throw new NotImplementedException();
    }

    public static Cell operator-(Cell lhs, Cell rhs)
    {
        return new Cell(
            (lhs.x - lhs.y / 2) - (rhs.x - rhs.y / 2), 
            lhs.y - rhs.y);
    }

    public static Cell operator+(Cell lhs, Cell rhs)
    {
        var y = lhs.y + rhs.y;
        return new Cell(
            lhs.x + rhs.x + y / 2 - lhs.y / 2,
            y);
    }

    public bool Equals(Cell other)
    {
        return this.x == other.x && this.y == other.y;
    }

    public override string ToString()
    {
        return string.Format("{0},{1}", x, y);
    }

    public static Cell zero = new Cell(0, 0);
}


class Unit : IEquatable<Unit>, ICloneable
{
    public List<Cell> members { get; set; }
    public Cell pivot { get; set; }

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
                return move(new Cell(-1, 0));
            case Constants.Northwest:
                return move(new Cell(1, 1));
            default:
            case Constants.Northeast:
                return move(new Cell(1, 0));
        }
    }

    public Unit move(Cell direction)
    {
        return new Unit()
        {
            members = this.members.Select(i => i + direction).ToList(),
            pivot = this.pivot + direction
        };
    }

    public Unit rotate(bool clockwise = true)
    {
        return new Unit()
        {
            members = this.members.Select(i => (i - this.pivot).rotate(clockwise) + this.pivot).ToList(),
            pivot = pivot
        };
    }

    public Unit center(int width)
    {
        int y_min = members.Min(i => i.y);
        int x_min = members.Min(i => i.x);
        int x_max = members.Max(i => i.x);
        return move(new Cell((width - x_max + x_min) / 2, -y_min));
    }

    public override string ToString()
    {
        return string.Join(",", members.Select(i => string.Format("[{0},{1}]", i.x, i.y)));
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
            }
        }
    }
    
    public string inputFilename { get; set; }
    public TimeSpan timeLimit { get; set; }
    public int megabyteLimit { get; set; }
    public List<string> phrasesOfPower { get; set; }
    public int cores { get; set; }
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

    static AnnotatedOutput solve(Input input, UInt32 seed)
    {
        var source = input.units.shuffle(new Random(seed));

        var tree = new BoardTree(new Board(input.width, input.height));

        foreach (var piece_ in source)
        {
            Console.WriteLine("solving {0}", tree);
            var piece = piece_.center(input.width);

            bool finished = true;
            tree.walk(i => finished &= !i.expand(piece));
            tree.prune(Constants.PruneTreeLimit);

            if (finished)
            {
                break;
            }
        }

        // TODO: get deepest result.
        BoardTree ans = tree.getBestLeafNodes().First();
        string solution = "";
        ans.walkFromRoot(i => solution = solution + i.path);

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

    static void Main(string[] args)
    {
        var bt = new BoardTree(new Board(10, 10));
        var start = JsonConvert.DeserializeObject<Unit>("{'members':[{'x':1,'y':3},{'x':1,'y':2},{'x':1,'y':1}],'pivot':{'x':1,'y':2}}");
        var end = JsonConvert.DeserializeObject<Unit>("{'members':[{'x':5,'y':0},{'x':4,'y':1},{'x':5,'y':2}],'pivot':{'x':4,'y':1}}");
        bt.generatePath(start, end);

        var commandLineParams = new CommandLineParams(args);

        var input = JsonConvert.DeserializeObject<Input>(
            File.ReadAllText(commandLineParams.inputFilename));

        var output = input.sourceSeeds
            .Select(seed => solve(input, seed))
            .ToList();

        Console.WriteLine(JsonConvert.SerializeObject(output));
    }
}
