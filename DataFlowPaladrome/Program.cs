﻿using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Dynamic;
using System.Linq;
using System.Net;
using System.Security;
using System.Text;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;

namespace DataFlowPaladrome
{
    class Program
    {
        static void Main(string[] args)
        {
            //Create the memeber of the pipeline

            //Download the requested resource as a string
            var downloadString = new TransformBlock<string, string>(uri =>
            {
                Console.WriteLine("Downloading '{0}'...", uri);

                return new WebClient().DownloadString(uri);
            });

            //Seperates the specified text into an aray of words
            var createWordList = new TransformBlock<string, string[]>(text =>
            {
                Console.WriteLine("Creating word list...");

                //Remove common punctuation by replacing all non-letter characters with a space character
                char[] tokens = text.ToArray();
                for (int i = 0; i < tokens.Length; i++)
                {
                    if (!char.IsLetter(tokens[i]))
                    {
                        tokens[i] = ' ';
                    }
                }

                text = new string(tokens);

                //Seperate the text into an array of words
                return text.Split(new [] {' '}, StringSplitOptions.RemoveEmptyEntries);
            });

            //Removes short words, orders the resulting words alphabetically, and then removes dups
            var filterWordList = new TransformBlock<string[], string[]>(words =>
            {
                Console.WriteLine("Filtering word list...");

                return words.Where(word => word.Length > 3).OrderBy(word => word).Distinct().ToArray();
            });

            //Finds all words in the specified collection whose reverse also exists in the collection
            var findPalindromes = new TransformManyBlock<string[], string>(words =>
            {
                Console.WriteLine("Finding palindromes...");

                //Holds palindromes
                var palindromes = new ConcurrentQueue<string>();

                //Add each word in the original collection to the result whose palindrome also exists in the collection
                Parallel.ForEach(words, word =>
                {
                    //Reverse the work
                    var reverse = new string(word.Reverse().ToArray());

                    //Enqueue the word if the reversed verion also exists in the collection
                    if (Array.BinarySearch(words, reverse) >= 0 && word != reverse)
                    {
                        palindromes.Enqueue(word);
                    }
                });

                return palindromes;
            });

            //Prints the provised palidrome to the console
            var printPalindrome = new ActionBlock<string>(palindrome => Console.WriteLine("Found palindrome {0}/{1}", palindrome, new string(palindrome.Reverse().ToArray())));
        
            //Connect the dataflow blocks to form a pipeline

            downloadString.LinkTo(createWordList);
            createWordList.LinkTo(filterWordList);
            filterWordList.LinkTo(findPalindromes);
            findPalindromes.LinkTo(printPalindrome);

            //For each completion task in the pipeline, create a task
            //that marks the next block in the pipeline as completed
            //A completed dataflow block processes any buffered elements, but does
            // not accept new elements.

            downloadString.Completion.ContinueWith(t =>
            {
                if (t.IsFaulted) ((IDataflowBlock) createWordList).Fault(t.Exception);
                else createWordList.Complete();
            });
            createWordList.Completion.ContinueWith(t =>
            {
                if (t.IsFaulted) ((IDataflowBlock) filterWordList).Fault(t.Exception);
                else filterWordList.Complete();
            });
            filterWordList.Completion.ContinueWith(t =>
            {
                if (t.IsFaulted) ((IDataflowBlock) findPalindromes).Fault(t.Exception);
                else findPalindromes.Complete();
            });
            findPalindromes.Completion.ContinueWith(t =>
            {
                if (t.IsFaulted) ((IDataflowBlock) printPalindrome).Fault(t.Exception);
                else printPalindrome.Complete();
            });

            //Process "THe Iliad of Homer" by Homer
            downloadString.Post("http://www.gutenberg.org/files/6130/6130-0.txt");

            //Mark the head of the piplene as complete. The continuation tasks
            //propagate completion through the piple as each part of the pipeline finishes
            downloadString.Complete();

            //Wait for the last block in the pipeline to process all messages.
            printPalindrome.Completion.Wait();
        }
    }
}
