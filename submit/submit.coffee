fs = require('fs')
execFile = require('child_process').execFile

g = JSON.parse(
    fs.readFileSync('../work/g.json'), { encoding: 'utf8' })

apiToken = 'rTtrTF3dLjQK/pN16VMLkg6zxstoXlwOUa06jqRVr48='
teamId = 183
problems = ('problem_' + i for i in [0 .. 23])
phrasesOfPower = ['Ei!']
concurrency = 8

runOne = (problem, cb) ->
    cmd = []
    cmd.push('-f')
    cmd.push "../problems/#{problem}.json"
    for phrase in phrasesOfPower
        cmd.push('-p')
        cmd.push(phrase)

    execFile '../solve/bin/Release/solve.exe', cmd, null, (error, stdout, stderr) ->
        throw error if error?
        
        answers = JSON.parse(stdout)
        
        for ans in answers
            problemKey = "#{problem}-#{ans.output.seed}"
            data = g.solutions[problemKey] or { history: [] }
            data.history.push(ans)
            
            bestScore = (if data.best? then data.best.score else 0)
            newHigh = (if ans.score > bestScore then '[NEW HIGH]' else if ans.score is bestScore '[HIGH]' else '')
            console.log "#{problemKey}: #{bestScore} -> #{ans.score} #{newHigh}"
            if ans.score > bestScore
                data.best = ans
                #TODO: submit solution
            
            g.solutions[problemKey] = data
        
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
    #fs.writeFileSync('../work/g.json', JSON.stringify(g))

runner = new Runner(problems, onComplete)
for i in [1 .. concurrency] 
    runner.startOne()