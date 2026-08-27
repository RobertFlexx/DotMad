#!/usr/bin/env dotmad
(letfun fizzbuzz (n)
  (do
    (letfun aux (i)
      (if (> i n)
        unit
        (do
          (println
            (switch 0
              ((= (mod i 15) 0) "FizzBuzz")
              ((= (mod i 3) 0) "Fizz")
              ((= (mod i 5) 0) "Buzz")
              (_ (to_string i))))
          (aux (+ i 1)))))
    (aux 1)))

(fizzbuzz 20)
