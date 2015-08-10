os = require('os')
fs = require('fs')
execFile = require('child_process').execFile

g = JSON.parse(
    fs.readFileSync('../work/g.json'), { encoding: 'utf8' })

if process.argv.length is 2
    for key in Object.keys(g.solutions).sort()
        best = g.solutions[key].best
        console.log "#{key}: #{best.score} #{best.time} #{(best.commit or "undefined").substring(0,10)} #{best.timeLimitSeconds}"
    return;
    
problemId = process.argv[2]
if (process.argv.length is 4)
    item = g.solutions[problemId].best
else
    item = g.solutions[problemId].history[g.solutions[problemId].history.length - 1]

console.log "Score: #{item.score}"

cmd = []
cmd.push('-f')
cmd.push "../problems/problem_#{item.output.problemId}.json"
cmd.push('-r')
cmd.push item.output.seed
cmd.push('-s')
cmd.push item.output.solution

execFile '../solve/bin/Release/solve.exe', cmd, { maxBuffer: 1024 * 1024 * 1024 }, (error, stdout, stderr) ->
    throw error if error?
    console.log stdout
    console.log item
    