#!/usr/bin/env dotmad
(letfun count (start end)
  (if (> start end)
    (println "done!")
    (do
      (println start)
      (count (+ start 1) end))))

(count 1 10)
