#!/usr/bin/env dotmad
(let add
  (lambda (x)
    (lambda (y)
      (+ x y))))

(let add10 (add 10))
(let result (add10 20))

(println "add10(20) = " result)
(if (= result 30)
  (println "Closures work!")
  (println "Something is wrong!"))
