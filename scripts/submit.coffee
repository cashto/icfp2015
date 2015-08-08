os = require('os')
fs = require('fs')
execFile = require('child_process').execFile

g = JSON.parse(
    fs.readFileSync('../work/g.json'), { encoding: 'utf8' })

apiToken = 'rTtrTF3dLjQK/pN16VMLkg6zxstoXlwOUa06jqRVr48='
teamId = 183
problemFiles = ('problem_' + i for i in [0 .. 23])

problems = []
for file in problemFiles
    input = JSON.parse(
        fs.readFileSync("../problems/#{file}.json"), { encoding: 'utf8' })
    for seed in input.sourceSeeds
        problems.push { file: file, seed: seed }

phrasesOfPower = ['Ei!']
concurrency = os.cpus().length

upload = (problemKey, ans) ->
    args = [
        '--user', ':' + apiToken,
        '-X', 'POST',
        '-H', 'Content-Type: application/json',
        '-d', JSON.stringify(ans),
        "https://davar.icfpcontest.org/teams/#{teamId}/solutions"
    ]
    
    try
        execFile 'curl', args, null, (error, stdout, stderr) ->
            console.log "   #{problemKey}: #{stdout} (#{error})"
    catch e
        console.log e
        
runOne = (problem, cb) ->
    cmd = []
    cmd.push('-f')
    cmd.push "../problems/#{problem.file}.json"
    cmd.push('-r')
    cmd.push problem.seed
    cmd.push('-t')
    cmd.push('60')
    for phrase in phrasesOfPower
        cmd.push('-p')
        cmd.push(phrase)

    execFile './solve.exe', cmd, null, (error, stdout, stderr) ->
        throw error if error?
        
        answers = JSON.parse(stdout)
        
        for ans in answers
            problemKey = "#{problem.file}-#{ans.output.seed}"
            data = g.solutions[problemKey] or { history: [] }
            data.history.push(ans)
            
            bestScore = (if data.best? then data.best.score else 0)
            newHigh = '' 
            newHigh = '[NEW HIGH]' if ans.score > bestScore
            newHigh = '[HIGH]' if ans.score is bestScore
            
            console.log "#{problemKey}: #{bestScore} -> #{ans.score} #{newHigh}"
            
            if ans.score >= bestScore
                data.best = ans
                upload(problemKey, [ans.output])
            
            g.solutions[problemKey] = data
            fs.writeFileSync('../work/g.json', JSON.stringify(g))
            
        cb()

class Runner
    constructor: (@problems, @cb) ->
        @index = 0
        
    startOne: ->
        if @index >= @problems.length
            @cb()
        else
            runOne(@problems[@index++], => @startOne())

onComplete = ->
    fs.writeFileSync('../work/g.json', JSON.stringify(g))

runner = new Runner(problems, onComplete)
for i in [1 .. concurrency] 
    runner.startOne()

#for problem in problems
#    ans = []
#    for key,value of g.solutions
#        if key.substr(0, problem.length + 1) == problem + '-'
#            ans.push(value.best.output)
#    #console.log ans
#    #console.log '------'
    
#    upload("", ans)
#    #console.log JSON.stringify(value.best.output)