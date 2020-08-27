﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

namespace GrammarAnalyzer.Kernel
{
    public class LLGrammar : Grammar
    {
        private Dictionary<Token, int> _rs = new Dictionary<Token, int>();
        private Dictionary<Token, int> _cs = new Dictionary<Token, int>();
        private Dictionary<ValueTuple<int, int>, List<Prodc>> _sheet = new Dictionary<ValueTuple<int, int>, List<Prodc>>();
        //private Dictionary<KeyValuePair<int, int>, List<Prodc>> _dpsheet = new Dictionary<KeyValuePair<int, int>, List<Prodc>>();

        private void EliminateLeftRecursion()
        {
            List<Token> nonterms = _nonterms.ToList();
            for (int i = 0; i < nonterms.Count; ++i)
            {
                Token token = nonterms[i];
                List<List<Token>> alphas = new List<List<Token>>();
                List<List<Token>> betas = new List<List<Token>>();
                _prodcs.RemoveWhere(e =>
                {
                    if (e._left == token)
                    {
                        if (e._right.First() == token)
                        {
                            alphas.Add(new List<Token>(e._right.Skip(1).ToList()));
                            return true;
                        }
                        else if (e._right.First() != Epsilon)
                        {
                            betas.Add(e._right);
                            return false;
                        }
                        else
                        {
                            return false;
                        }
                    }
                    return false;
                });

                if (alphas.Count > 0)
                {
                    Token dot = new Token(token);
                    do
                    {
                        dot._attr += '\'';
                    } while (_tokens.Contains(dot));
                    _nonterms.Add(dot); _tokens.Add(dot); nonterms.Add(dot);
                    alphas.ForEach(e => e.Add(new Token(dot)));
                    alphas.ForEach(e => _prodcs.Add(new Prodc(dot, e)));

                    betas.ForEach(e => e.Add(new Token(dot)));
                    betas.ForEach(e => _prodcs.Add(new Prodc(token, e)));
                }
            }
        }

        private void EliminateCommonLeftTokens()
        {
            List<Token> nonterms = _nonterms.ToList();
            bool noCommonTokens = false;
            do
            {
                noCommonTokens = true;
                for (int i = 0; i < nonterms.Count; ++i)
                {
                    Token token = nonterms[i];
                    List<List<Token>> cans = new List<List<Token>>();
                    foreach (var prodc in _prodcs)
                    {
                        if (prodc._left == token)
                        {
                            cans.Add(prodc._right);
                        }
                    }
                    List<int> merge = new List<int>();
                    List<int> rest = new List<int>();
                    for (int main = 0; main != cans.Count; ++main)
                    {
                        for (int sub = 0; sub != cans.Count; ++sub)
                        {
                            if (main != sub && cans[main].First() == cans[sub].First())
                            {
                                merge.Add(sub);
                            }
                            else if (main != sub && cans[sub].First() != Epsilon)
                            {
                                rest.Add(sub);
                            }
                        }
                        if (merge.Count > 0)
                        {
                            merge.Add(main);
                            break;
                        }
                        else
                        {
                            rest.Clear();
                        }
                    }
                    if (merge.Count > 0)
                    {
                        Token dot = new Token(token);
                        do
                        {
                            dot._attr += '\'';
                        } while (_tokens.Contains(dot));
                        _nonterms.Add(dot); _tokens.Add(dot); nonterms.Add(dot);

                        _prodcs.Add(new Prodc(token, new List<Token> { cans[merge.First()].First(), dot }));
                        merge.ForEach(e =>
                        {
                            _prodcs.Remove(new Prodc(token, cans[e]));
                            if (cans[e].Count == 1)
                            {
                                _prodcs.Add(new Prodc(dot, new List<Token> { Epsilon }));
                                _terms.Add(Epsilon); _tokens.Add(Epsilon);
                            }
                            else
                            {
                                _prodcs.Add(new Prodc(dot, cans[e].Skip(1).ToList()));
                            }
                        });
                        noCommonTokens = false;
                    }
                }
            } while (!noCommonTokens);
        }

        private Dictionary<Token, List<Token>> GetFirstSet()
        {
            Dictionary<Token, List<Token>> fis = new Dictionary<Token, List<Token>>();
            return fis;
        }

        private Dictionary<Token, List<Token>> GetFollowSet(Dictionary<Token, List<Token>> fis)
        {
            Dictionary<Token, List<Token>> fos = new Dictionary<Token, List<Token>>();
            return fos;
        }

        public LLGrammar(Grammar raw)
        {
            _tokens = raw.Tokens;
            _terms = raw.Terms;
            _nonterms = raw.Nonterms;
            _prodcs = raw.Prodcs;
            _start = raw.Start;
            // build LL-syntax analyzer
            Extend();
            EliminateLeftRecursion();
            EliminateCommonLeftTokens();
        }

        public Dictionary<ValueTuple<int, int>, List<Prodc>> BuildAnalysisSheet()
        {
            if (_fis.Count == 0 || _fos.Count == 0)
                return null;
            _rs = new Dictionary<Token, int>();
            _cs = new Dictionary<Token, int>();

            // reset row and column indices
            int index = 0;
            foreach (var item in _nonterms)
            {
                _rs.Add(item, index++);
            }

            index = 0;
            foreach (var item in _terms)
            {
                _cs.Add(item, index++);
            }
            foreach (var item in _nonterms)
            {
                _cs.Add(item, index++);
            }

            foreach (var prodc in _prodcs)
            {
                // ri, li must own values
                _rs.TryGetValue(prodc._left, out int ri);

                HashSet<Token> fis = SubFIS(prodc._right);
                foreach (var elem in fis)
                {
                    if (elem == Epsilon)
                    {
                        // need follow set
                        _fos.TryGetValue(prodc._left, out HashSet<Token> fos);
                        foreach (var token in fos)
                        {
                            // ri, li must own values
                            _cs.TryGetValue(token, out int li);

                            bool added = false;
                            foreach (var unit in _sheet)
                            {
                                if (unit.Key == (ri, li))
                                {
                                    unit.Value.Add(prodc);
                                    added = true;
                                }
                            }
                            if (!added)
                            {
                                _sheet.Add((ri, li), new List<Prodc> { prodc });
                            }
                        }
                    }
                    else
                    {
                        // ri, li must own values
                        _cs.TryGetValue(elem, out int li);

                        bool added = false;
                        foreach (var unit in _sheet)
                        {
                            if (unit.Key == (ri, li))
                            {
                                unit.Value.Add(prodc);
                                added = true;
                            }
                        }
                        if (!added)
                        {
                            _sheet.Add((ri, li), new List<Prodc> { prodc });
                        }
                    }
                }
            }

            return _sheet;
        }
    }
}