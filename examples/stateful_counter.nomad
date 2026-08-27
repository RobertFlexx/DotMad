#!/usr/bin/env dotmad
(letfun make_counter ()
  (do
    (let x 0)
    (letfun incr ()
      (do (mut x (+ x 1)) x))
    (letfun decr ()
      (do (mut x (- x 1)) x))
    (record (incr incr) (decr decr))))

(let c (make_counter))
(println "incr: " ((. c incr)))
(println "incr: " ((. c incr)))
(println "incr: " ((. c incr)))
(println "decr: " ((. c decr)))
(println "decr: " ((. c decr)))
(println "final: " ((. c incr)))
