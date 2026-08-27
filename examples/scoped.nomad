#!/usr/bin/env dotmad
(println
  (scoped
    ((x 10)
     (y 20))
    (+ x y)))

(try
  (println x " This was unexpected!")
  (println "This was expected!"))
