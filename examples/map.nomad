#!/usr/bin/env dotmad
(let numbers (list 1 2 3 4 5 6 7 8 9 10))
(let squared (map (lambda (x) (* x x)) numbers))

(println "Original: " numbers)
(println "Squared:  " squared)
(println "Length:   " (len squared))
