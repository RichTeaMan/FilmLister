﻿using FilmLister.Domain;
using System;
using System.Linq;

namespace FilmLister.Service
{
    /// <summary>
    /// Sorts an array using QuickSort algorithm. Code shamelessly derived from https://rosettacode.org/wiki/Sorting_algorithms/Quicksort#C.23.
    /// </summary>
    /// <typeparam name="T"></typeparam>
    public class QuickSort<T> : ISortAlgorithm<T> where T : AbstractComparable<T>
    {
        private const int INSERTION_LIMIT_DEFAULT = 12;

        public int InsertionLimit { get; set; }
        private Random Random { get; set; }
        private T Median { get; set; }

        private int Left { get; set; }
        private int Right { get; set; }
        private int LeftMedian { get; set; }
        private int RightMedian { get; set; }

        #region Constructors
        public QuickSort(int insertionLimit, Random random)
        {
            InsertionLimit = insertionLimit;
            Random = random;
        }

        public QuickSort(int insertionLimit)
          : this(insertionLimit, new Random())
        {
        }

        public QuickSort()
          : this(INSERTION_LIMIT_DEFAULT)
        {
        }
        #endregion

        #region Sort Methods
        public SortResult<T> Sort(T[] entries)
        {
            return Sort(entries, 0, entries.Length - 1);
        }

        public SortResult<T> Sort(T[] entries, int first, int last)
        {
            var length = last + 1 - first;
            while (length > 1)
            {
                if (length < InsertionLimit)
                {
                    var sortResult = InsertionSort<T>.Sort(entries, first, last);
                    if (!sortResult.Completed)
                    {
                        return sortResult;
                    }
                }

                Left = first;
                Right = last;
                var pivotResult = Pivot(entries);
                if (!pivotResult.Completed)
                {
                    return pivotResult;
                }
                var partitionResult = Partition(entries);
                if (!partitionResult.Completed)
                {
                    return partitionResult;
                }
                //[Note]Right < Left

                var leftLength = Right + 1 - first;
                var rightLength = last + 1 - Left;

                //
                // First recurse over shorter partition, then loop
                // on the longer partition to elide tail recursion.
                //
                if (leftLength < rightLength)
                {
                    var sortResult = Sort(entries, first, Right);
                    if (!sortResult.Completed)
                    {
                        return sortResult;
                    }
                    first = Left;
                    length = rightLength;
                }
                else
                {
                    var sortResult = Sort(entries, Left, last);
                    if (!sortResult.Completed)
                    {
                        return sortResult;
                    }
                    last = Right;
                    length = leftLength;
                }
            }
            return new SortResult<T>(entries);
        }

        private SortResult<T> Pivot(T[] entries)
        {
            // An odd sample size is chosen based on the log of the interval size.
            // The median of a randomly chosen set of samples is then returned as
            // an estimate of the true median.
            //
            var length = Right + 1 - Left;
            var logLen = (int)Math.Log10(length);
            var pivotSamples = 2 * logLen + 1;
            var sampleSize = Math.Min(pivotSamples, length);
            var last = Left + sampleSize - 1;
            // Sample without replacement
            for (var first = Left; first <= last; first++)
            {
                // Random sampling avoids pathological cases
                var random = Random.Next(first, Right + 1);
                Swap(entries, first, random);
            }

            var insertionResult = InsertionSort<T>.Sort(entries, Left, last);
            if (!insertionResult.Completed)
            {
                return insertionResult;
            }
            Median = entries[Left + sampleSize / 2];
            return new SortResult<T>(entries);
        }

        private SortResult<T> Partition(T[] entries)
        {
            var first = Left;
            var last = Right;
            LeftMedian = first;
            RightMedian = last;
            while (true)
            {
                //[Assert]There exists some index >= Left where entries[index] >= Median
                //[Assert]There exists some index <= Right where entries[index] <= Median
                // So, there is no need for Left or Right bound checks

                // while (Median.CompareTo(entries[Left]) > 0) Left++;
                while (true)
                {
                    var compareResult = Median.AbstractCompareTo(entries[Left]);
                    if (!compareResult.ComparisonSucceeded)
                    {
                        return new SortResult<T>(entries, entries[Left], Median);
                    }
                    if (compareResult.ComparisonResult > 0)
                    {
                        Left++;
                    }
                    else
                    {
                        break;
                    }
                }

                //while (Median.CompareTo(entries[Right]) < 0) Right--;
                while (true)
                {
                    var compareResult = Median.AbstractCompareTo(entries[Right]);
                    if (!compareResult.ComparisonSucceeded)
                    {
                        return new SortResult<T>(entries, entries[Right], Median);
                    }
                    if (compareResult.ComparisonResult < 0)
                    {
                        Right--;
                    }
                    else
                    {
                        break;
                    }
                }

                //[Assert]entries[Right] <= Median <= entries[Left]
                if (Right <= Left) break;

                Swap(entries, Left, Right);
                if (!SwapOut(entries))
                {
                    return new SortResult<T>(entries, entries[Left], entries[Right]);
                }
                Left++;
                Right--;
                //[Assert]entries[first:Left - 1] <= Median <= entries[Right + 1:last]
            }

            if (Left == Right)
            {
                Left++;
                Right--;
            }
            //[Assert]Right < Left
            SwapIn(entries, first, last);

            //[Assert]entries[first:Right] <= Median <= entries[Left:last]
            //[Assert]entries[Right + 1:Left - 1] == Median when non-empty

            return new SortResult<T>(entries);
        }
        #endregion

        #region Swap Methods
        private bool SwapOut(T[] entries)
        {
            // if (Median.CompareTo(entries[Left]) == 0) Swap(entries, LeftMedian++, Left);
            var leftCompare = Median.AbstractCompareTo(entries[Left]);
            if (!leftCompare.ComparisonSucceeded)
            {
                return false;
            }
            if (leftCompare.ComparisonResult == 0)
            {
                Swap(entries, LeftMedian++, Left);
            }

            // if (Median.CompareTo(entries[Right]) == 0) Swap(entries, Right, RightMedian--);
            var rightCompare = Median.AbstractCompareTo(entries[Right]);
            if (!rightCompare.ComparisonSucceeded)
            {
                return false;
            }
            if (rightCompare.ComparisonResult == 0)
            {
                Swap(entries, Right, RightMedian--);
            }
            return true;
        }

        private void SwapIn(T[] entries, int first, int last)
        {
            // Restore Median entries
            while (first < LeftMedian) Swap(entries, first++, Right--);
            while (RightMedian < last) Swap(entries, Left++, last--);
        }

        public static void Swap(T[] entries, int index1, int index2)
        {
            if (index1 != index2)
            {
                var entry = entries[index1];
                entries[index1] = entries[index2];
                entries[index2] = entry;
            }
        }
        #endregion
    }

    #region Insertion Sort
    internal static class InsertionSort<T> where T : AbstractComparable<T>
    {
        public static SortResult<T> Sort(T[] entries, int first, int last)
        {
            T[] result = entries.ToArray();
            for (var index = first + 1; index <= last; index++)
            {
                var insertionResult = Insert(result, first, index);
                if (!insertionResult.Completed)
                {
                    return insertionResult;
                }
                result = insertionResult.SortedResults.ToArray();
            }
            for (int index = 0; index < entries.Length; index++)
            {
                entries[index] = result[index];
            }
            return new SortResult<T>(entries);
        }

        private static SortResult<T> Insert(T[] entries, int first, int index)
        {
            T[] result = entries.ToArray();
            var entry = result[index];
            while (index > first)
            {
                var compareResult = result[index - 1].AbstractCompareTo(entry);
                if (!compareResult.ComparisonSucceeded)
                {
                    return new SortResult<T>(entries, result[index - 1], entry);
                }

                if (compareResult.ComparisonResult > 0)
                {
                    result[index] = result[--index];
                }
                else
                {
                    break;
                }
            }
            result[index] = entry;
            return new SortResult<T>(result);
        }

        public class InsertionResult
        {
            public bool Succeeded { get; }

            public T[] Entries { get; }

            public InsertionResult(T[] entries)
            {
                Succeeded = true;
                Entries = entries ?? throw new ArgumentNullException(nameof(entries));
            }

            private InsertionResult()
            {
                Succeeded = false;
                Entries = null;
            }

            public static InsertionResult FailedResult()
            {
                return new InsertionResult();
            }
        }

    }
    #endregion
}