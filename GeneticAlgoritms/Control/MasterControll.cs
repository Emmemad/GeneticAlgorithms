﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Collections.Concurrent;
using System.Threading;

namespace GeneticAlgorithms
{
    public class FlagException : Exception {}

    public class MasterControll
    {
        private object _locker;
        private bool _is_change;
        private ManualResetEvent stopper;
        public int MigrationCount { get; private set; }
        public double MigrationProbability { get; private set; }
        public bool StopState { get; private set; }
        public Task[] IselandTasks;
        public Control[] Controls;

        public bool IsAvaliableChange { 
            get {
                if (!Monitor.TryEnter(_locker, 100))
                {
                    throw new FlagException();//эквивалентно false
                }
                try
                {
                    return _is_change;
                }
                finally
                {
                    Monitor.Exit(_locker);
                }
            }
            private set {
                _is_change = value;
            }
        }

        public void Set() {//start ring
            lock (_locker) {
                if (_is_change == false) { throw new FlagException(); }
                IsAvaliableChange = false;
            }
        }
        public void Reset() {//stop ring
            lock (_locker) {
                if (_is_change == true) { throw new FlagException(); }
                IsAvaliableChange = true;
            }
        }

        public MasterControll(int migration_count, int population_count, string xml, int generationSize, Func<int, Delegates.Crossover> get_cross, Func<int, Delegates.Mutator> get_mutate, Func<int, Action<Control>> onNewStep)
        {
            MigrationCount = migration_count;
            _locker = new object();
            _is_change = true;
            MigrationProbability = 1.0d /(population_count*10.0d);
            StopState = true;
            stopper = new ManualResetEvent(StopState);
            ChangeBag<AbstractIndividual>[] changebags = new ChangeBag<AbstractIndividual>[population_count];
            for (int i = 0; i < population_count; i++) {
                changebags[i] = new ChangeBag<AbstractIndividual>();
            }
            Controls = new Control[population_count];
            IselandTasks = new Task[population_count];
            Controls[0] = new Control(this, xml, generationSize, changebags[population_count - 1], changebags[0]);
            IselandTasks[0] = new Task(() => TaskFunc(Controls[0], get_cross(0), get_mutate(0), onNewStep(0)));
            //IselandTasks[0].Start();
            for (int j = 1; j < population_count; j++) {
                int iterator = j;
                Controls[iterator] = new Control(this, xml, generationSize, changebags[iterator - 1], changebags[iterator]);
                IselandTasks[iterator] = new Task(() => TaskFunc(Controls[iterator], get_cross(iterator), get_mutate(iterator), onNewStep(iterator)));
                //IselandTasks[i].Start();
            }
            Console.WriteLine();
        }
        public void Start()
        {
            foreach (Task t in IselandTasks)
            {
                t.Start();
            }
        }

        public void ResetStopper() {
            if (StopState == false) { return; }
            stopper.Reset();//осановка
            StopState = false;
        }
        public void SetStopper() {
            if (StopState == true) { return; }
            stopper.Set();//запуск
            StopState = true;
        }

        private void TaskFunc(Control c, Delegates.Crossover cross, Delegates.Mutator mutate, Action<Control> onNewStep)
        {
            while (true)
            {
                stopper.WaitOne();
                c.OptimizeStep(cross, mutate);
                onNewStep(c);
            }
        }
    }
}
