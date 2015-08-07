using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Text;
using System.Threading.Tasks;


static class Constants
{
    public static int GenerateGoalsMaxExamined = 100;
    public static int GenerateGoalsMaxReturned = 20;
    public static int ParseTreeLimit = 20;
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
        return 
            !this.contains(piece.move(Cell.east)) ||
            !this.contains(piece.move(Cell.west)) ||
            !this.contains(piece.move(Cell.southeast)) ||
            !this.contains(piece.move(Cell.southwest)) ||
            !this.contains(piece.rotate(false)) ||
            !this.contains(piece.rotate(true));
    }

    public void place(IEnumerable<Cell> cells)
    {
        // TODO return new object
        // TODO drop lines
        foreach (var cell in cells)
        {
            data[cell.x + cell.y * width] = true;
        }
    }

    public bool this[int x, int y]
    {
        get
        {
            return this.data[x + y * width];
        }
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
    }

    public int CompareTo(BoardTree other)
    {
        return other.board.score - this.board.score;
    }

    public bool expand(Unit unit)
    {
        if (children != null)
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
        if (children != null)
        {
            return;
        }

        fn(this);

        foreach (var child in children)
        {
            child.walk(fn);
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

    public IEnumerable<BoardTree> getBestNodes()
    {
        var allNodes = new List<BoardTree>();
        walk(i => allNodes.Add(i));
        allNodes.Sort();
        return allNodes;
    }

    public void prune(int n)
    {
        walk(i => i.mark = true);

        foreach (var node in getBestNodes().Take(n))
        {
            node.walkFromRoot(i => i.mark = false);
        }

        walk(boardTree => boardTree.children = boardTree.children.Where(child => !child.mark).ToList());
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
                    foreach (var cell in rotatedPiece.members)
                    {
                        var movedPiece = rotatedPiece.move(new Cell(x - cell.x, y - cell.y));
                        if (board.contains(movedPiece) && !ans.Contains(movedPiece) && board.canLock(movedPiece))
                        {
                            ans.Add(movedPiece);
                        }
                    }

                    rotatedPiece = rotatedPiece.rotate();
                }
            }

            if (ans.Count > Constants.GenerateGoalsMaxExamined)
            {
                break;
            }
        }

        return ans.shuffle(new Random((UInt32)DateTime.UtcNow.Ticks)).Take(Constants.GenerateGoalsMaxReturned);
    }

    private BoardTree generatePath(Unit start, Unit end)
    {
        throw new NotImplementedException();
    }

    public Board board;
    public string path;

    private BoardTree parent;
    private List<BoardTree> children;
    private bool mark;
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

    public static Cell operator-(Cell lhs, Cell rhs)
    {
        return new Cell(lhs.x - rhs.x, lhs.y - rhs.y);
    }

    public static Cell operator +(Cell lhs, Cell rhs)
    {
        return new Cell(lhs.x + rhs.x, lhs.y + rhs.y);
    }

    public bool Equals(Cell other)
    {
        return this.x == other.x && this.y == other.y;
    }

    public static Cell zero = new Cell(0, 0);
    public static Cell west = new Cell(-1, 0);
    public static Cell east = new Cell(1, 0);
    public static Cell southwest = new Cell(-1, 1);
    public static Cell southeast = new Cell(1, 1);
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
            }
        }
    }
    
    public string inputFilename { get; set; }
    public TimeSpan timeLimit { get; set; }
    public int megabyteLimit { get; set; }
    public List<string> phrasesOfPower { get; set; }
}


public static class Program
{
    static void Main(string[] args)
    {
        var commandLineParams = new CommandLineParams(args);
        
        var input = JsonConvert.DeserializeObject<Input>(
            File.ReadAllText(commandLineParams.inputFilename));

        var output = new List<AnnotatedOutput>();
        foreach (var seed in input.sourceSeeds)
        {
            solve(input, seed);
        }

        Console.WriteLine(JsonConvert.SerializeObject(output));
    }

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
            var piece = piece_.center(input.width);

            bool finished = true;
            tree.walk(i => finished &= !i.expand(piece));
            tree.prune(Constants.ParseTreeLimit);

            if (finished)
            {
                break;
            }
        }

        BoardTree ans = tree.getBestNodes().First();
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
}
