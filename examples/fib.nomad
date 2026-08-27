#!/usr/bin/env dotmad
(letfun fib (n)
  (switch n
    (0 0)
    (1 1)
    (_ (+ (fib (- n 1)) (fib (- n 2))))))

(println "Fibonacci sequence:")
(foreach (lambda (n) (print (fib n) " "))
  (range 0 15 (list_init 16 (lambda (i) i))))
(println "")

(println "fib(30) = " (fib 30))
