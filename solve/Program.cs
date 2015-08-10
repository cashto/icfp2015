using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Text;
using System.Threading.Tasks;


static class Constants
{
    public const int LookaheadSearchPly = 4;
    public const int LookaheadSearchDepth = 3;

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
    public const string DownMoves = "aghij4lmno 5";

    public static List<string> PhrasesOfPower;
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
            j >= board.height ||
            i >= 0 && i < board.width &&
            j >= 0 && j < board.height &&
            board[i, j] ? 1 : 0;

        int uppermostNonemptyRow = board.height - 1;
        squaredRowCount = 0;

        neighborCount = 0;
        for (var y = board.height - 1; y >= 0; --y)
        {
            bool isOddLine = (y & 1) != 0;
            var rowCount = 0;
            for (var x = 0; x < board.width; ++x)
            {
                if (!board[x, y])
                {
                    continue;
                }

                ++rowCount;
                uppermostNonemptyRow = y;

                int sw = x - (isOddLine ? 0 : 1);
                int se = x + (isOddLine ? 1 : 0);

                neighborCount +=
                    isOccupied(sw, y + 1) +
                    isOccupied(se, y + 1) +
                    isOccupied(x - 1, y) +
                    isOccupied(x + 1, y);
            }
            squaredRowCount += rowCount * rowCount;
        }

        int targetRow = (uppermostNonemptyRow == board.height - 1) ? board.width : board.lineCount(uppermostNonemptyRow + 1);
        neighborCount /= 4;
    }

    public override string ToString()
    {
        return string.Format("{0}.{1}.{2}", score, neighborCount, squaredRowCount);
    }

    public int CompareTo(GoalHeuristic other)
    {
        // We care about things in this order:
        //    1. Maximize score.
        //    2. Maximize (E, W, SE, SW) neighbor count (to encourage clumping).
        //    3. Maximize the number of filled cells in the second-most-northmost row 
        //      (worth five neighbors per cell).

        if (this.score == other.score)
        {
            if (other.neighborCount == this.neighborCount)
            {
                return other.squaredRowCount - this.squaredRowCount;
            }

            return other.neighborCount - this.neighborCount;
        }

        return other.score - this.score;
    }

    public int score;
    public int neighborCount;
    public int squaredRowCount;
}


class Board
{
    public Board(Input input)
    {
        this.score = 0;
        this.width = input.width;
        this.height = input.height;
        this.data = new bool[width * height];
        this.usedPhrasesOfPower = new List<string>();
        
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
        int lineBonus = parent.linesRemoved > 1 ? (parent.linesRemoved - 1) * points / 10 : 0;
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

    public void addPhraseOfPower(string phrase)
    {
        if (!this.usedPhrasesOfPower.Contains(phrase))
        {
            bonusPoints += 300;
            this.usedPhrasesOfPower.Add(phrase);
        }

        bonusPoints += 2 * phrase.Length;
    }

    public Unit findEndCell(Unit piece, string phrase)
    {
        if (!contains(piece))
        {
            return null;
        }

        foreach (var c in phrase)
        {
            piece = piece.go(c);

            if (!contains(piece))
            {
                return null;
            }
        }

        return piece;
    }

    public HashSet<Unit> getIllegalSet(Unit piece, string path)
    {
        var set = new HashSet<Unit>();

        set.Add(piece);
        foreach (char c in path)
        {
            piece = piece.go(c);
            set.Add(piece);
        }

        return set;
    }

    public IEnumerable<Unit> getPossibleLocations(Unit piece)
    {
        var pieceWidth = piece.members.Max(i => i.x) - piece.members.Min(i => i.x);
        var pieceHeight = piece.members.Max(i => i.y) - piece.members.Min(i => i.y);

        for (var y = piece.members.Min(i => i.y); y < this.height - pieceHeight; ++y)
        {
            for (var i = 0; i < piece.symmetry; ++i)
            {
                piece = piece.rotate();
                piece = piece.leftJustify();

                for (var x = 0; x < this.width - pieceWidth; ++x)
                {
                    yield return piece;
                    piece = piece.move(new Cell(1, 0));
                }
            }

            piece = piece.move(new Cell(0, 1));
        }
    }

    public override string ToString()
    {
        return "\n" + ToString(null);
    }

    public string ToString(Unit piece = null)
    {
        var sb = new StringBuilder();

        for (var y = 0; y < height; ++y)
        {
            if ((y & 1) != 0)
            {
                sb.Append(' ');
            }

            for (var x = 0; x < width; ++x)
            {
                bool isPiece = piece != null && piece.members.Contains(new Cell(x, y));
                sb.Append(isPiece ? '#' : this[x, y] ? 'o' : '.');
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

    public int width;
    public int height;
    public int score;
    public int bonusPoints;
    public int linesRemoved;
    public GoalHeuristic heuristicScore;
    public List<string> usedPhrasesOfPower;

    private bool[] data;
}


class BoardTree
{
    public BoardTree(Board board, string path = "", BoardTree parent = null, Unit start = null, Unit end = null)
    {
        this.parent = parent;
        this.board = board;
        this.path = path;
        this.start = start;
        this.end = end;
        this.level = (parent == null ? 0 : parent.level + 1);
    }

    public override string ToString()
    {
        return string.Format("level={0}, score={1}, heuristic={2}", level, this.board.score, this.board.heuristicScore);
    }

    public BoardTree solve(List<Unit> pieces, CommandLineParams commandLineParams)
    {
        var ply = Constants.LookaheadSearchPly;
        var currentNode = this;
        var startTime = DateTime.UtcNow;
        var lastUpdate = startTime;

        while (currentNode.level + Constants.LookaheadSearchDepth < pieces.Count &&
            DateTime.UtcNow - startTime < commandLineParams.timeLimit)
        {
            if (!commandLineParams.timeLimit.Equals(DateTime.MaxValue) &&
                DateTime.UtcNow - lastUpdate > TimeSpan.FromTicks(commandLineParams.timeLimit.Ticks / 10))
            {
                var fractionComplete = currentNode.level / (double)pieces.Count;
                var fractionTimeUsed = (DateTime.UtcNow.AddSeconds(5) - startTime).Ticks / (double)commandLineParams.timeLimit.Ticks;

                if (fractionTimeUsed > fractionComplete)
                {
                    ply = Math.Max(ply - 1, 2);
                }
                else
                {
                    ply = Math.Min(ply + 1, 6);
                }

                lastUpdate = DateTime.UtcNow;
            }

            Program.Log(ply.ToString());
            
            for (var i = 0; i < Constants.LookaheadSearchDepth; ++i)
            {
                currentNode.walk(node => node.expand(pieces[node.level], ply));
            }
            
            var bestNode = currentNode.findBestLeafNode();
            while (bestNode == null || bestNode == currentNode)
            {
                // We've hit a dead end: time to backtrack.
                currentNode.isDisappointment = true;

                for (var i = 0; i < Constants.LookaheadSearchDepth; ++i)
                {
                    if (currentNode.parent == null)
                    {
                        // Can't backtrack any more. We've REALLY hit a dead end.
                        return findBestLeafNode(true /*includeDisappointments*/);
                    }

                    currentNode = currentNode.parent;
                }
                
                bestNode = currentNode.findBestLeafNode();
            }

            currentNode = bestNode.walkToRoot().Skip(Constants.LookaheadSearchDepth - 1).First();

            foreach (var node in currentNode.walkToRoot().Skip(3).Reverse())
            {
                node.insertPhrasesOfPowerAndLockingMove();
                if (commandLineParams.cutoff.HasValue &&
                    node.board.score + node.board.bonusPoints > commandLineParams.cutoff.Value)
                {
                    return findBestLeafNode(true /*includeDisappointments*/);
                }
            }
        }
        
        return findBestLeafNode(true /*includeDisappointments*/);
    }

    private bool expand(Unit unit, int ply)
    {
        if (children != null)
        {
            return false;
        }

        if (!board.contains(unit))
        {
            return false;
        }

        // Program.Log("Expanding {0}", this);
        var inaccessibleSet = new HashSet<Unit>();

        this.children = generateGoals(unit)
            .Select(goal => generatePath(this.board, unit, goal, inaccessibleSet))
            .Where(child => child != null)
            .Take(ply)
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

    public IEnumerable<BoardTree> walkToRoot()
    {
        for (var node = this; node != null; node = node.parent)
        {
            yield return node;
        }
    }

    public string getFullPath()
    {
        string ans = "";
        foreach (var i in walkToRoot().Reverse())
        {
            ans += i.insertPhrasesOfPowerAndLockingMove().path;
        }
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

        return ans.OrderBy(i => i.Item1).Select(i => i.Item2);
    }

    public BoardTree generatePath(
        Board board,
        Unit start, 
        Unit end, 
        HashSet<Unit> inaccessibleSet, 
        HashSet<Unit> extraIllegalSet = null)
    {
        if (start.Equals(end))
        {
            return new BoardTree(board, "", this, start, end);
        }

        if (extraIllegalSet != null && extraIllegalSet.Contains(start))
        {
            return null;
        }

        var pq = new PriorityQueue<GeneratePathNode>((i, j) => i.score(start) > j.score(start));
        var set = new HashSet<Unit>();

        var myInaccessibleSet = new HashSet<Unit>();
        var rootNode = new GeneratePathNode(end);
        pq.push(rootNode);

        // Program.Log("{0} -> {1}", end, start);

        while (!pq.isEmpty())
        {
            var item = pq.pop();
            myInaccessibleSet.Add(item.Piece);
            if (inaccessibleSet.Contains(item.Piece))
            {
                break;
            }

            if (extraIllegalSet != null && extraIllegalSet.Contains(item.Piece))
            {
                continue;
            }

            var illegalSet = new HashSet<Unit>();
            item.getIllegalSet(illegalSet);

            // Program.Log("{0}, score={1}", item, item.score(start));

            foreach (var c in Constants.ReverseMoves)
            {
                var newNode = new GeneratePathNode(item, c);

                if (newNode.Piece.Equals(start))
                {
                    return new BoardTree(
                        new Board(board, end),
                        newNode.getReversePath(),
                        this,
                        start,
                        end);
                }

                if (board.contains(newNode.Piece) && 
                    !illegalSet.Contains(newNode.Piece))
                {
                    if (!set.Contains(newNode.Piece))
                    {
                        set.Add(newNode.Piece);
                        pq.push(newNode);
                    }
                }
            }
        }

        inaccessibleSet.UnionWith(myInaccessibleSet); 
        return null;
    }

    private BoardTree insertPhrasesOfPowerAndLockingMove()
    {
        if (this.insertedPhrasesOfPower)
        {
            return this;
        }

        if (this.parent == null)
        {
            return this;
        }

        // Program.Log("inserting phrase of power {0}", this);

        this.board.usedPhrasesOfPower = this.parent.board.usedPhrasesOfPower.ToList();
        this.board.bonusPoints = this.parent.board.bonusPoints;
        var path = "";

        this.oldPath = this.path;

        // Insert as many phrases of power as possible.
        var start = this.start;
        var extraIllegalSet = new HashSet<Unit>();
        while (insertOnePhraseOfPower(ref path, ref start, this.end))
        {
        }

        var endPath = generatePath(this.parent.board, start, this.end, new HashSet<Unit>());
        path += endPath.path;

        // Add locking move.
        path += Constants.ForwardMoves.First(dir => !this.parent.board.contains(this.end.go(dir)));
        
        this.path = path;
        this.insertedPhrasesOfPower = true;

        return this;
    }

    private bool insertOnePhraseOfPower(
        ref string path, 
        ref Unit start, 
        Unit end)
    {
        var inaccessibleSet = new HashSet<Unit>();
        var emptySet = new HashSet<Unit>();

        // Prefer to try unused phrases first.
        var unusedPhrases = new List<string>();
        var usedPhrases = new List<string>();
        foreach (var phrase in Constants.PhrasesOfPower)
        {
            (this.board.usedPhrasesOfPower.Contains(phrase) ? usedPhrases : unusedPhrases).Add(phrase);
        } 
        
        foreach (var phrase in unusedPhrases.Concat(usedPhrases))
        {
            foreach (var phraseStart in board.getPossibleLocations(start))
            {
                var phraseEnd = this.parent.board.findEndCell(phraseStart, phrase);
                if (phraseEnd == null)
                {
                    continue;
                }

                if (phraseEnd.pivot.y > end.pivot.y)
                {
                    break;
                }

                // Optimization: generate one path with the empty set of illegal moves in order to populate the inaccessible set.
                var test = this.generatePath(this.parent.board, start, phraseStart, inaccessibleSet, emptySet);
                if (test == null)
                {
                    continue;
                }

                var illegalSet = new HashSet<Unit>();
                addToIllegalSet(illegalSet, phraseStart, phrase);
                illegalSet.Remove(phraseStart);
                addToIllegalSet(illegalSet, this.start, path);

                var pathToPhrase = this.generatePath(this.parent.board, start, phraseStart, new HashSet<Unit>(), illegalSet);
                if (pathToPhrase == null)
                {
                    continue;
                }

                // Special case: if pathToPhrase is the null path, then phrase might be illegal right after path.
                if (pathToPhrase.path.Length == 0 &&
                    checkAgainstIllegalSet(new HashSet<Unit>(), start, path + phrase))
                {
                    continue;
                }

                addToIllegalSet(illegalSet, start, pathToPhrase.path);
                illegalSet.Add(phraseStart);

                var pathAfterPhrase = this.generatePath(this.parent.board, phraseEnd, end, new HashSet<Unit>(), illegalSet);
                if (pathAfterPhrase == null)
                {
                    continue;
                }

                // Sanity check.
                //var totalPath = path + pathToPhrase.path + phrase + pathAfterPhrase.path;
                //if (checkAgainstIllegalSet(
                //    new HashSet<Unit>(),
                //    this.start,
                //    totalPath))
                //{
                //    throw new Exception();
                //}

                //var t = this.start;
                //foreach (var c in totalPath)
                //{
                //    t = t.go(c);
                //    if (!this.parent.board.contains(t))
                //    {
                //        throw new Exception();
                //    }
                //}

                //if (!t.Equals(this.end))
                //{
                //    throw new Exception();
                //}

                path += pathToPhrase.path + phrase;
                start = phraseEnd;
                board.addPhraseOfPower(phrase);
                return true;
            }
        }

        return false;
    }

    private static void addToIllegalSet(HashSet<Unit> illegalSet, Unit piece, string path)
    {
        foreach (var c in path)
        {
            illegalSet.Add(piece);
            piece = piece.go(c);
        }
    }

    private static bool checkAgainstIllegalSet(HashSet<Unit> illegalSet, Unit piece, string path)
    {
        foreach (var c in path)
        {
            piece = piece.go(c);
            if (illegalSet.Contains(piece))
            {
                return true;
            }
            
            illegalSet.Add(piece);
        }

        return false;
    }


    private static char reverseDir(char c)
    {
        switch (c)
        {
            case Constants.East: return Constants.West;
            case Constants.Northeast: return Constants.Southwest;
            case Constants.Northwest: return Constants.Southeast;
            case Constants.West: return Constants.East;
            case Constants.Southwest: return Constants.Northeast;
            case Constants.Southeast: return Constants.Northwest;
            case Constants.Clockwise: return Constants.CounterClockwise;
            case Constants.CounterClockwise: return Constants.Clockwise;
            default:
                throw new Exception();
        }
    }

    private BoardTree findBestLeafNode(bool includeDisappointments = false)
    {
        BoardTree ans = null;
        GoalHeuristic bestHeuristic = null;
        
        walk(i =>
        {
            if (i.children != null)
            {
                return;
            }

            if (bestHeuristic == null || i.board.heuristicScore.CompareTo(bestHeuristic) < 0)
            {
                if (includeDisappointments || !i.isDisappointment)
                {
                    bestHeuristic = i.board.heuristicScore;
                    ans = i;
                }
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

        public string getReversePath()
        {
            var sb = new StringBuilder();
            for (var node = this; node.Parent != null; node = node.Parent)
            {
                sb.Append(reverseDir(node.Move));
            }
            return sb.ToString();
        }

        public override string ToString()
        {
            return string.Format("piece={0}, rpath={1}", this.Piece, getReversePath());
        }
    }

    public Board board;
    public string path;
    public string oldPath;

    public BoardTree parent;
    private List<BoardTree> children;
    private bool isDisappointment;
    private int level;
    private Unit start;
    public Unit end;
    private bool insertedPhrasesOfPower;
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

    public Unit go (string s)
    {
        Unit ans = this;
        foreach (var c in s)
        {
            ans = ans.go(c);
        }
        return ans;
    }

    public Unit go(char direction)
    {
        switch (Program.canonicalize(direction))
        {
            case Constants.West: return move(new Cell(-1, 0));
            case Constants.East: return move(new Cell(1, 0));
            case Constants.Southwest: return move(new Cell(-1, 1));
            case Constants.Southeast: return move(new Cell(0, 1));
            case Constants.Clockwise: return rotate(true);
            case Constants.CounterClockwise: return rotate(false);
            case Constants.Northwest: return move(new Cell(0, -1));
            case Constants.Northeast: return move(new Cell(1, -1));
            default: return move(new Cell(0, 0));
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

    public Unit leftJustify()
    {
        int x_min = members.Min(i => i.x);

        Func<Cell, Cell> adjust = (cell) =>
            new Cell(cell.x - x_min, cell.y);

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
        int hash = pivot.x + (pivot.y << 8);
        foreach (var cell in members)
        {
            hash += (cell.x << 16);
            hash += (cell.y << 24);
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

                case "-x":
                    argsEnum.MoveNext();
                    cutoff = int.Parse(argsEnum.Current);
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
    public int? cutoff{ get; set; }
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

    public static char canonicalize(char c)
    {
        switch (char.ToLowerInvariant(c))
        {
            case 'p':
            case '\'':
            case '!':
            case '.':
            case '0':
            case '3':
                return Constants.West;

            case 'b':
            case 'c':
            case 'e':
            case 'f':
            case 'y':
            case '2':
                return Constants.East;

            case 'a':
            case 'g':
            case 'h':
            case 'i':
            case 'j':
            case '4':
                return Constants.Southwest;

            case 'l':
            case 'm':
            case 'n':
            case 'o':
            case ' ':
            case '5':
                return Constants.Southeast;

            case 'd':
            case 'q':
            case 'r':
            case 'v':
            case 'z':
            case '1':
                return Constants.Clockwise;

            case 'k':
            case 's':
            case 't':
            case 'u':
            case 'w':
            case 'x':
                return Constants.CounterClockwise;

            default:
                return c;
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
        var ans = tree.solve(source, commandLineParams);
        string solution = ans.getFullPath();

        foreach (var node in ans.walkToRoot().Reverse())
        {
            Program.Log(node.ToString());
            if (node.board.linesRemoved > 1 || (node.parent != null && node.parent.board.linesRemoved > 1))
            {
                Program.Log(node.board.linesRemoved.ToString());
            }

        //    Program.Log(node.oldPath ?? "none"); 
        //    Program.Log(node.path);
            Program.Log(node.board.ToString(node.end));
            Program.Log("");
        }

        return new AnnotatedOutput()
        {
            score = ans.board.score + ans.board.bonusPoints,
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

        Unit piece = null;
        var sourceEnum = source.GetEnumerator();
        var move = 0;

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
                if (!board.contains(piece))
                {
                    Console.WriteLine("----- End of game -----");
                    break;
                }
            }

            var nextPiece = piece.go(c);
            if (!board.contains(nextPiece))
            {
                var nextBoard = new Board(board, piece);
                ++move;
                Console.WriteLine("move={0}, score={1}", move, nextBoard.heuristicScore);
                Console.WriteLine(board.ToString(piece));
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

        Constants.PhrasesOfPower = commandLineParams.phrasesOfPower
            .OrderByDescending(i => (10000 * i.Length) / i.ToLowerInvariant().Count(c => Constants.DownMoves.Contains(c)))
            .ToList();

        var output = input.sourceSeeds
            .Where(seed => !commandLineParams.randomSeed.HasValue || seed == commandLineParams.randomSeed.Value)
            .Select(seed => solve(commandLineParams, input, seed))
            .ToList();

        // show(input, output.First().output.seed, output.First().output.solution);

        Console.WriteLine(JsonConvert.SerializeObject(output, Formatting.Indented, new JsonSerializerSettings() { NullValueHandling = NullValueHandling.Ignore }));
    }

    public static void Log(string format, params object[] data)
    {
        // Console.WriteLine(format, data);
    }
}
