#!/usr/bin/env dotmad
(letfun quicksort (l)
  (if (= l ())
    ()
    (do
      (let pivot (car l))
      (let rest (cdr l))
      (let smaller (filter (lambda (x) (< x pivot)) rest))
      (let bigger (filter (lambda (x) (>= x pivot)) rest))
      (append
        (append (quicksort smaller) (list pivot))
        (quicksort bigger)))))

(let numbers (list 38 27 43 3 9 82 10))
(println "Original: " numbers)
(println "Sorted:   " (quicksort numbers))
