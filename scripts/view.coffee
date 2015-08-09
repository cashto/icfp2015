os = require('os')
fs = require('fs')
execFile = require('child_process').execFile

g = JSON.parse(
    fs.readFileSync('../work/g.json'), { encoding: 'utf8' })

if process.argv.length isnt 3
    for key in Object.keys(g.solutions).sort()
        best = g.solutions[key].best
        console.log "#{key}: #{best.score} #{best.time} #{(best.commit or "undefined").substring(0,10)} #{best.timeLimitSeconds}"
    return;
    
problemId = process.argv[2]
console.log "Score: #{g.solutions[problemId].best.score}"

cmd = []
cmd.push('-f')
cmd.push "../problems/problem_#{g.solutions[problemId].best.output.problemId}.json"
cmd.push('-r')
cmd.push g.solutions[problemId].best.output.seed
cmd.push('-s')
cmd.push g.solutions[problemId].best.output.solution

execFile '../solve/bin/Release/solve.exe', cmd, { maxBuffer: 1024 * 1024 * 1024 }, (error, stdout, stderr) ->
    throw error if error?
    console.log stdout
    