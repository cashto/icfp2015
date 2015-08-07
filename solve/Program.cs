using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Text;
using System.Threading.Tasks;


class Random
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


class Piece
{
    public Piece(Board board, Unit unit)
    {
        this.unit = (Unit)unit.Clone();
        this.board = board;
        this.history = new List<Unit>();
    }

    public bool canMove()
    {
        return false;
    }

    public bool rotate(bool clockwise = true)
    {
        return update(this.unit.rotate(clockwise));
    }

    public bool move(Cell direction)
    {
        return update(this.unit.move(direction));
    }

    private bool update(Unit newUnit)
    {
        var contained = board.contains(newUnit.members);
        
        if (contained)
        {
            unit = newUnit;
        }
        else
        {
            board.place(this.unit.members);
        }

        return contained;
    }

    private Unit unit;
    private Board board;
    private List<Unit> history;
}


class Board
{
    public Board(int width, int height, int score)
    {
        this.score = score;
        this.width = width;
        this.height = height;
        clear();
    }

    public void clear()
    {
        data = new bool[width * height];
    }

    public bool contains(IEnumerable<Cell> cells)
    {
        foreach (var cell in cells)
        {
            bool contained = 
                cell.x >= 0 &&
                cell.y >= 0 &&
                cell.x < width &&
                cell.y < height &&
                !data[cell.x + cell.y * width];
            
            if (!contained)
            {
                return false;
            }
        }

        return true;
    }

    public void place(IEnumerable<Cell> cells)
    {
        foreach (var cell in cells)
        {
            data[cell.x + cell.y * width] = true;
        }
    }

    private bool[] data;
    private int width;
    private int height;
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
        if (children == null)
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
        throw new NotImplementedException();
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
    public int x { get; set; }
    public int y { get; set; }

    public Cell rotate(bool clockwise = true)
    {
        int dir = clockwise ? 1 : -1;
        Func<int, int> odd = (a) => a & 1;
        int y2 = (dir * 4 * x + dir * 2 * odd(y) + 2 * y) / 4;
        int x2 = (2 * x + odd(y) - dir * y * 3 - odd(y2) * 2) / 4;
        return new Cell() { x = x2, y = y2 };
    }

    public static Cell operator-(Cell lhs, Cell rhs)
    {
        return new Cell() { x = lhs.x - rhs.x, y = lhs.y - rhs.y };
    }

    public static Cell operator +(Cell lhs, Cell rhs)
    {
        return new Cell() { x = lhs.x + rhs.x, y = lhs.y + rhs.y };
    }

    public bool Equals(Cell other)
    {
        return this.x == other.x && this.y == other.y;
    }

    public static Cell zero = new Cell() { x = 0, y = 0 };
    public static Cell west = new Cell() { x = -1, y = 0 };
    public static Cell east = new Cell() { x = 1, y = 0 };
    public static Cell southwest = new Cell() { x = -1, y = 1 };
    public static Cell southeast = new Cell() { x = 1, y = 1 };
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

    public Unit rotate(bool clockwise)
    {
        return new Unit()
        {
            members = this.members.Select(i => (i - this.pivot).rotate(clockwise) + this.pivot).ToList(),
            pivot = pivot
        };
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


class Program
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

    static IEnumerable<Unit> generateSource(List<Unit> units_, Random rand)
    {
        var units = units_.ToList();
        var ans = new List<Unit>(units.Count);
        
        while (units.Any())
        {
            int index = (int)(rand.next() % units.Count);
            ans.Add(units[index]);
            units.RemoveAt(index);
        }
        
        return ans;
    }

    static AnnotatedOutput solve(Input input, UInt32 seed)
    {
        var source = generateSource(input.units, new Random(seed));

        var tree = new BoardTree(new Board(input.width, input.height, 0));

        foreach (var piece in source)
        {
            bool finished = true;
            tree.walk(i => finished &= !i.expand(piece));
            tree.prune(20);

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
