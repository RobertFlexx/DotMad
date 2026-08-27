#!/usr/bin/env dotmad
(letfun factorial (n)
  (if (= n 0)
    1
    (* n (factorial (- n 1)))))

(println "Factorials:")
(foreach (lambda (n) (println n "! = " (factorial n)))
  (range 0 10 (list_init 11 (lambda (i) i))))
